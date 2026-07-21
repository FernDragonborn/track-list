using api.Services.External;
using api.Services.IServices;
using Microsoft.EntityFrameworkCore;
using api.DbContext;

namespace api.Services;

/// <summary>
///     Background worker for external content:
///     1) Every 24h: forces a refresh of the top-50 hottest media (by our review count).
///     2) Every 24h (independently): sweeps Letterboxd RSS for all configured handles and
///        attempts to match each item to a media in our DB, persisting ExternalReview rows.
/// </summary>
public class ExternalContentRefreshService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly LetterboxdRssClient _letterboxd;
	private readonly ILogger<ExternalContentRefreshService> _log;

	private static readonly TimeSpan Period = TimeSpan.FromHours(24);
	private const int TopN = 50;

	public ExternalContentRefreshService(IServiceScopeFactory scopeFactory, LetterboxdRssClient letterboxd, ILogger<ExternalContentRefreshService> log)
	{
		_scopeFactory = scopeFactory;
		_letterboxd = letterboxd;
		_log = log;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Wait 60s after boot to let DB / API stabilize.
		try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch { return; }

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await SweepTopMediaAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "Top-N external sweep failed");
			}

			try
			{
				await SweepLetterboxdAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "Letterboxd sweep failed");
			}

			try
			{
				await BackfillMissingAvatarsAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "External reviewer avatar backfill failed");
			}

			try { await Task.Delay(Period, stoppingToken); } catch { return; }
		}
	}

	/// <summary>
	///     Fill in AvatarUrl for reviewers that don't have one (new rows, or rows cleaned by
	///     the schema bridge). Scrapes Letterboxd profile pages with a throttle.
	/// </summary>
	private async Task BackfillMissingAvatarsAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TrackListDbContext>();
		var missing = await db.ExternalReviewers
			.Where(r => r.Source == "letterboxd" && r.AvatarUrl == null)
			.ToListAsync(ct);
		if (missing.Count == 0) return;

		_log.LogInformation("Backfilling avatars for {N} Letterboxd reviewers", missing.Count);
		foreach (var r in missing)
		{
			if (ct.IsCancellationRequested) break;
			var avatar = await _letterboxd.TryFetchAvatarUrlAsync(r.Handle, ct);
			if (avatar is not null)
			{
				r.AvatarUrl = avatar;
				r.LastSyncedAt = DateTime.UtcNow;
				await db.SaveChangesAsync(ct);
			}
			// Polite throttle — one profile page per second.
			await Task.Delay(1000, ct);
		}
	}

	private async Task SweepTopMediaAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TrackListDbContext>();
		var svc = scope.ServiceProvider.GetRequiredService<IExternalContentService>();

		// Top-N media by review count (excluding soft-deleted reviews via query filter).
		// EF Core expression tree: m.Reviews.Count() is required here because this
		// lambda is translated to SQL — EF emits a COUNT(*) subquery for the ORDER BY.
		// Replacing it with the ICollection<Review>.Count property (as S2971 suggests)
		// either throws "could not be translated" or forces EF to materialize every
		// Review for every Media into memory just to sort, which defeats the whole
		// point of database-side ordering.
#pragma warning disable S2971
		var topIds = await db.Media
			.OrderByDescending(m => m.Reviews.Count())
			.Select(m => m.Id)
			.Take(TopN)
			.ToListAsync(ct);
#pragma warning restore S2971

		_log.LogInformation("ExternalContentRefreshService: refreshing top {N} media", topIds.Count);
		foreach (var id in topIds)
		{
			if (ct.IsCancellationRequested) break;
			try
			{
				await svc.RefreshAsync(id, ct);
				await Task.Delay(1000, ct); // 1 req/sec — well under OMDb 1000/day budget
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "Top-N refresh failed for {Id}", id);
			}
		}
	}

	private async Task SweepLetterboxdAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TrackListDbContext>();
		var mediaGet = scope.ServiceProvider.GetRequiredService<IMediaGetService>();
		var reviewers = scope.ServiceProvider.GetRequiredService<IExternalReviewerService>();

		// Build (title, year) → mediaId map from our DB once for efficient matching.
		var mediaIndex = await db.Media
			.Include(m => m.Translations)
			.Select(m => new { m.Id, m.Type, m.ReleaseYear, Titles = m.Translations
				.Where(t => t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
				.Select(t => t.Title!).Where(t => t != null).ToList() })
			.ToListAsync(ct);

		int matched = 0;
		int imported = 0;
		foreach (var handle in LetterboxdRssClient.DefaultHandles)
		{
			if (ct.IsCancellationRequested) break;
			var items = await _letterboxd.FetchFeedAsync(handle, ct);
			foreach (var item in items)
			{
				var keyTitle = item.FilmTitle.Trim();
				var keyYear = item.FilmYear;

				var match = mediaIndex.FirstOrDefault(m => m.Titles.Any(t =>
					string.Equals(t, keyTitle, StringComparison.OrdinalIgnoreCase))
					&& (keyYear is null || m.ReleaseYear == keyYear));

				// Not in DB? RSS carries a tmdb:movieId — auto-import via the same path
				// the catalog uses when a user opens an unseen TMDB media. This turns
				// the RSS sweep from a passive matcher into a discovery channel.
				if (match is null && item.TmdbMovieId is { } tmdbId)
				{
					var externalId = $"Tmdb:movie:{tmdbId}";
					var importRes = await mediaGet.GetByIdAsync(externalId, ct);
					if (importRes.IsSuccess && importRes.Value is { } importedMedia)
					{
						var importedTitles = importedMedia.Translations?
							.Where(t => t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
							.Select(t => t.Title!).Where(t => t != null).ToList() ?? new List<string>();
						match = new { importedMedia.Id, importedMedia.Type, importedMedia.ReleaseYear, Titles = importedTitles };
						mediaIndex.Add(match);
						imported++;
						_log.LogInformation("Letterboxd sweep auto-imported {Title} ({Year}) via {ExternalId}",
							keyTitle, keyYear, externalId);
					}
				}

				if (match is null) continue;

				// Upsert ExternalReview
				var refId = $"{handle}:{item.Guid}";
				var existing = await db.ExternalReviews.FirstOrDefaultAsync(r => r.Source == "letterboxd" && r.ExternalRefId == refId, ct);

				var content = StripHtml(item.SummaryHtml).Trim();
				if (string.IsNullOrWhiteSpace(content) || content.Length < 30)
				{
					continue;
				}
				if (content.Length > 9900) content = content[..9900] + "…";

				int? rating = item.Rating.HasValue ? (int)Math.Round(item.Rating.Value * 2) : (int?)null;
				if (rating is { } r && (r < 1 || r > 10)) rating = null;

				// Get-or-create the virtual reviewer profile so the FK column is always populated.
				var reviewer = await reviewers.GetOrCreateAsync("letterboxd", handle, ct);

				if (existing is null)
				{
					db.ExternalReviews.Add(new ExternalReview
					{
						Id = Guid.NewGuid(),
						MediaId = match.Id,
						Source = "letterboxd",
						ExternalRefId = refId,
						AuthorHandle = handle,
						AuthorUrl = $"https://letterboxd.com/{handle}/",
						ExternalReviewerId = reviewer.Id,
						Content = content,
						Rating = rating,
						LikeCountOnSource = null,
						SourceUrl = item.Link,
						PublishedAt = item.Published,
						FetchedAt = DateTime.UtcNow,
					});
					matched++;
				}
				else
				{
					existing.Content = content;
					existing.Rating = rating;
					existing.ExternalReviewerId = reviewer.Id;
					existing.FetchedAt = DateTime.UtcNow;
				}
			}
			await db.SaveChangesAsync(ct);
			// throttle between handles
			await Task.Delay(2000, ct);
		}

		_log.LogInformation("Letterboxd sweep complete — {N} new matches", matched);
	}

	private static string StripHtml(string html)
	{
		if (string.IsNullOrEmpty(html)) return string.Empty;
		var noTags = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");
		var decoded = System.Net.WebUtility.HtmlDecode(noTags);
		var collapsed = System.Text.RegularExpressions.Regex.Replace(decoded, @"\s+", " ").Trim();
		return collapsed;
	}
}
