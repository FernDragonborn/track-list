using System.Text.Json;
using api.Models;
using api.Enums;
using api.Services.External;
using api.Services.IServices;
using api.Repository.IReposotory;
using api.Utils;
using Microsoft.EntityFrameworkCore;
using api.DbContext;
using dotenv.net;

namespace api.Services;

public class ExternalContentService : IExternalContentService
{
	private readonly IUnitOfWork _uow;
	private readonly OmdbClient _omdb;
	private readonly WikipediaClient _wiki;
	private readonly LetterboxdRssClient _letterboxd;
	private readonly ILogger<ExternalContentService> _log;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IHttpClientFactory _httpFactory;
	private readonly string? _tmdbKey;
	private readonly bool _tmdbEnabled;
	private readonly bool _anyExternalEnabled;

	// Auto-translate target languages (additional UI languages → list grows here).
	private static readonly string[] AutoTargetLangs = { "uk" };

	public ExternalContentService(
		IUnitOfWork uow,
		OmdbClient omdb,
		WikipediaClient wiki,
		LetterboxdRssClient letterboxd,
		ILogger<ExternalContentService> log,
		IServiceScopeFactory scopeFactory,
		IHttpClientFactory httpFactory)
	{
		_uow = uow;
		_omdb = omdb;
		_wiki = wiki;
		_letterboxd = letterboxd;
		_log = log;
		_scopeFactory = scopeFactory;
		_httpFactory = httpFactory;
		var env = DotEnv.Read();
		_tmdbEnabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_TMDB");
		_anyExternalEnabled = SelfHostSecurityOptions.AnyExternalContentEnabled();
		_tmdbKey = Environment.GetEnvironmentVariable("TMDBApiKey") ?? (env.TryGetValue("TMDBApiKey", out var k) ? k : null);
	}

	public TimeSpan NextTtl(int fetchCount) => fetchCount switch
	{
		<= 1 => TimeSpan.FromHours(24),
		2 => TimeSpan.FromDays(3),
		_ => TimeSpan.FromDays(7),
	};

	public async Task<Dictionary<Guid, List<ExternalRatingDto>>> GetRatingsBatchAsync(IEnumerable<Guid> mediaIds, CancellationToken ct)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0) return new();
		var res = await _uow.ExternalRatingRepository.GetAsync(r => ids.Contains(r.MediaId));
		if (res.IsFailure) return new();
		return res.Value
			.GroupBy(r => r.MediaId)
			.ToDictionary(g => g.Key, g => g.Select(r => new ExternalRatingDto
			{
				Source = r.Source,
				Score = r.Score,
				RawScore = r.RawScore,
				VoteCount = r.VoteCount,
				FetchedAt = r.FetchedAt,
			}).ToList());
	}

	public async Task<Dictionary<Guid, MediaRatingsBatchEntryDto>> GetMediaRatingsBatchAsync(IEnumerable<Guid> mediaIds, CancellationToken ct)
	{
		var ids = mediaIds.Distinct().ToList();
		var result = ids.ToDictionary(id => id, _ => new MediaRatingsBatchEntryDto());
		if (ids.Count == 0) return result;

		// External ratings
		var extRes = await _uow.ExternalRatingRepository.GetAsync(r => ids.Contains(r.MediaId));
		if (extRes.IsSuccess)
		{
			foreach (var g in extRes.Value.GroupBy(r => r.MediaId))
			{
				if (!result.TryGetValue(g.Key, out var entry)) continue;
				entry.External = g.Select(r => new ExternalRatingDto
				{
					Source = r.Source,
					Score = r.Score,
					RawScore = r.RawScore,
					VoteCount = r.VoteCount,
					FetchedAt = r.FetchedAt,
				}).ToList();
			}
		}

		// Our aggregate (avg + count) per media — pull all reviews for these media at once
		var revRes = await _uow.ReviewRepository.GetAsync(r => ids.Contains(r.MediaId));
		if (revRes.IsSuccess)
		{
			foreach (var g in revRes.Value.GroupBy(r => r.MediaId))
			{
				if (!result.TryGetValue(g.Key, out var entry)) continue;
				entry.OurCount = g.Count();
				entry.OurAvg = entry.OurCount > 0 ? g.Average(r => (double)r.Rating) : (double?)null;
			}
		}

		return result;
	}

	public async Task<ExternalContentDto> GetForMediaAsync(Guid mediaId, CancellationToken ct)
	{
		var stateRes = await _uow.ExternalFetchStateRepository.GetOneAsync(s => s.MediaId == mediaId);
		var state = stateRes.IsSuccess ? stateRes.Value : null;

		var ratings = await LoadRatingsAsync(mediaId);
		var reviews = await LoadReviewsAsync(mediaId);
		var wiki = reviews.FirstOrDefault(r => r.Source == "wikipedia_reception");
		var nonWikiReviews = reviews.Where(r => r.Source != "wikipedia_reception").ToList();

		// state row is created at the START of RefreshAsync (before wiki + translation),
		// so existence alone isn't enough — wait for LastFetchedAt to be set at the END.
		var status = ResolveStatus(state);

		// Load cached translations for all involved entities in one go.
		var translationEntityIds = new List<(string Type, string RefId)>();
		if (wiki is not null) translationEntityIds.Add(("external_review", wiki.Id.ToString()));
		foreach (var r in nonWikiReviews) translationEntityIds.Add(("external_review", r.Id.ToString()));
		translationEntityIds.Add(("media_description", mediaId.ToString()));

		var translationLookup = await LoadTranslationsAsync(translationEntityIds);

		var dto = new ExternalContentDto
		{
			Status = status,
			Ratings = ratings.Select(r => new ExternalRatingDto
			{
				Source = r.Source,
				Score = r.Score,
				RawScore = r.RawScore,
				VoteCount = r.VoteCount,
				FetchedAt = r.FetchedAt,
			}).ToList(),
			WikiReception = wiki is null ? null : new WikiReceptionDto
			{
				Id = wiki.Id,
				Content = wiki.Content,
				SourceUrl = wiki.SourceUrl,
				FetchedAt = wiki.FetchedAt,
				Translations = translationLookup.GetValueOrDefault(("external_review", wiki.Id.ToString())),
			},
			Reviews = nonWikiReviews.Select(r => new ExternalReviewDto
			{
				Id = r.Id,
				Source = r.Source,
				AuthorHandle = r.AuthorHandle,
				AuthorUrl = r.AuthorUrl,
				Content = r.Content,
				Rating = r.Rating,
				LikeCountOnSource = r.LikeCountOnSource,
				SourceUrl = r.SourceUrl,
				PublishedAt = r.PublishedAt,
				FetchedAt = r.FetchedAt,
				Translations = translationLookup.GetValueOrDefault(("external_review", r.Id.ToString())),
				Reviewer = r.ExternalReviewer is null ? null : new ExternalReviewerDto
				{
					Id = r.ExternalReviewer.Id,
					Source = r.ExternalReviewer.Source,
					Handle = r.ExternalReviewer.Handle,
					DisplayName = r.ExternalReviewer.DisplayName,
					Bio = r.ExternalReviewer.Bio,
					AvatarUrl = r.ExternalReviewer.AvatarUrl,
					SourceProfileUrl = r.ExternalReviewer.SourceProfileUrl,
					LastSyncedAt = r.ExternalReviewer.LastSyncedAt,
				},
			}).ToList(),
			DescriptionTranslations = translationLookup.GetValueOrDefault(("media_description", mediaId.ToString())),
			LastFetchedAt = state?.LastFetchedAt,
			NextFetchDueAt = state?.NextFetchDueAt,
			LastError = state?.LastError,
		};

		// Trigger background refresh if missing or due
		var needsRefresh = state is null || (state.NextFetchDueAt is null) || (state.NextFetchDueAt < DateTime.UtcNow);
		if (_anyExternalEnabled && needsRefresh)
		{
			QueueBackgroundRefresh(mediaId);
		}
		else if (!_anyExternalEnabled)
		{
			dto.Status = "disabled";
		}

		return dto;
	}

	private async Task<Dictionary<(string, string), Dictionary<string, string>>> LoadTranslationsAsync(List<(string Type, string RefId)> keys)
	{
		var result = new Dictionary<(string, string), Dictionary<string, string>>();
		if (keys.Count == 0) return result;
		var types = keys.Select(k => k.Type).Distinct().ToList();
		var refIds = keys.Select(k => k.RefId).Distinct().ToList();
		var res = await _uow.TranslationRepository.GetAsync(t =>
			types.Contains(t.EntityType) && refIds.Contains(t.EntityRefId));
		if (res.IsFailure) return result;
		foreach (var grp in res.Value.GroupBy(t => (t.EntityType, t.EntityRefId)))
		{
			result[grp.Key] = grp.ToDictionary(t => t.TargetLang, t => t.Content);
		}
		return result;
	}

	public async Task<bool> RefreshAsync(Guid mediaId, CancellationToken ct)
	{
		var mediaRes = await _uow.MediaRepository.GetOneAsync(m => m.Id == mediaId, "Translations");
		if (mediaRes.IsFailure)
		{
			_log.LogWarning("RefreshAsync: media {Id} not found", mediaId);
			return false;
		}
		var media = mediaRes.Value;

		var titleEn = media.Translations
			.Where(t => t.LanguageCode == "en" && (t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved))
			.Select(t => t.Title)
			.FirstOrDefault();
		var anyTitle = titleEn ?? media.Translations.FirstOrDefault()?.Title;

		string? imdbId = await ResolveImdbIdAsync(media, ct);

		// Use a fresh scope so we can write atomically and avoid context disposal issues if caller already disposed.
		using var scope = _scopeFactory.CreateScope();
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

		// Ensure state row
		var stateRes = await uow.ExternalFetchStateRepository.GetOneAsync(s => s.MediaId == mediaId);
		ExternalFetchState state;
		if (stateRes.IsSuccess)
		{
			state = stateRes.Value;
		}
		else
		{
			state = new ExternalFetchState { Id = Guid.NewGuid(), MediaId = mediaId, FetchCount = 0 };
			await uow.ExternalFetchStateRepository.AddAsync(state);
			await uow.SaveAsync();
		}

		var ok = true;
		var errors = new List<string>();

		// --- OMDb (ratings) ---
		if (!string.IsNullOrEmpty(imdbId) && _omdb.IsConfigured)
		{
			try
			{
				var omdbRatings = await _omdb.FetchAsync(imdbId!, ct);
				if (omdbRatings is { Count: > 0 })
				{
					foreach (var r in omdbRatings)
					{
						await UpsertRatingAsync(uow, mediaId, r.Source, r.Score, r.RawScore, r.VoteCount);
					}
					await uow.SaveAsync();
				}
			}
			catch (Exception ex)
			{
				ok = false;
				errors.Add($"omdb: {ex.Message}");
				_log.LogWarning(ex, "OMDb refresh failed for media {Id}", mediaId);
			}
		}

		// --- Wikipedia reception ---
		if (!string.IsNullOrEmpty(anyTitle))
		{
			try
			{
				var mtype = media.Type == MediaType.Series ? "series" : "film";
				var wiki = await _wiki.FetchReceptionAsync(anyTitle!, media.ReleaseYear, mtype, ct);
				if (wiki is not null)
				{
					var refId = $"wiki:{anyTitle}:{media.ReleaseYear}".ToLowerInvariant();
					await UpsertReviewAsync(uow, mediaId, "wikipedia_reception", refId,
						authorHandle: "Wikipedia",
						authorUrl: wiki.ArticleUrl,
						content: wiki.Content,
						rating: null,
						likeCount: null,
						sourceUrl: wiki.ArticleUrl,
						publishedAt: null);
					await uow.SaveAsync();

					// Look up the persisted ExternalReview to get its Id for the translation key.
					var savedRes = await uow.ExternalReviewRepository.GetOneAsync(
						r => r.Source == "wikipedia_reception" && r.ExternalRefId == refId);
					if (savedRes.IsSuccess)
					{
						var translation = scope.ServiceProvider.GetRequiredService<ITranslationService>();
						foreach (var lang in AutoTargetLangs)
						{
							try
							{
								await translation.TranslateAndCacheAsync(
									"external_review", savedRes.Value.Id.ToString(), wiki.Content, lang, ct);
							}
							catch (Exception ex)
							{
								_log.LogWarning(ex, "Wiki auto-translate to {Lang} failed", lang);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				ok = false;
				errors.Add($"wiki: {ex.Message}");
				_log.LogWarning(ex, "Wikipedia refresh failed for media {Id}", mediaId);
			}
		}

		// --- TMDB description auto-translate ---
		// If only EN translation exists, generate UA cache via Translation table.
		try
		{
			var enDescription = media.Translations
				.Where(t => t.LanguageCode == "en"
					&& (t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved))
				.Select(t => t.Description)
				.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
			if (!string.IsNullOrWhiteSpace(enDescription))
			{
				var translation = scope.ServiceProvider.GetRequiredService<ITranslationService>();
				foreach (var lang in AutoTargetLangs)
				{
					try
					{
						await translation.TranslateAndCacheAsync(
							"media_description", mediaId.ToString(), enDescription!, lang, ct);
					}
					catch (Exception ex)
					{
						_log.LogWarning(ex, "Media description translate to {Lang} failed", lang);
					}
				}
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Description translation flow failed for media {Id}", mediaId);
		}

		// --- Letterboxd reviews: per-media on-demand match against cached RSS feeds ---
		// The 24h background sweep also runs, but newly imported media would otherwise
		// have to wait until the next sweep. This path runs the same match logic
		// against the in-memory feed cache so reviews appear on the first page load.
		try
		{
			await MatchLetterboxdForMediaAsync(scope, uow, media, ct);
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Letterboxd per-media match failed for {Id}", mediaId);
		}

		state = stateRes.IsSuccess ? stateRes.Value : state;
		state.FetchCount += 1;
		state.LastFetchedAt = DateTime.UtcNow;
		state.NextFetchDueAt = DateTime.UtcNow.Add(NextTtl(state.FetchCount));
		if (!ok)
		{
			state.LastErrorAt = DateTime.UtcNow;
			state.LastError = string.Join("; ", errors).Length > 500 ? string.Join("; ", errors)[..500] : string.Join("; ", errors);
		}
		await uow.ExternalFetchStateRepository.Update(state);
		await uow.SaveAsync();

		return ok;
	}

	private async Task MatchLetterboxdForMediaAsync(IServiceScope scope, IUnitOfWork uow, Media media, CancellationToken ct)
	{
		var titles = media.Translations
			.Where(t => t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
			.Select(t => t.Title!)
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.ToList();
		_log.LogInformation("Letterboxd per-media match: media={Id} titles=[{Titles}] year={Year}",
			media.Id, string.Join("|", titles), media.ReleaseYear);
		if (titles.Count == 0) return;

		var items = await _letterboxd.FindForFilmAsync(titles, media.ReleaseYear, ct);
		_log.LogInformation("Letterboxd per-media match: found {N} items for media={Id}", items.Count, media.Id);
		if (items.Count == 0) return;

		// Resolve reviewer (cached after first hit per handle within this scope)
		var reviewerCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

		foreach (var item in items)
		{
			var refId = $"{item.Handle}:{item.Guid}";
			var existingRes = await uow.ExternalReviewRepository.GetOneAsync(
				r => r.Source == "letterboxd" && r.ExternalRefId == refId);

			var content = StripHtml(item.SummaryHtml).Trim();
			if (string.IsNullOrWhiteSpace(content) || content.Length < 30) continue;
			if (content.Length > 9900) content = content[..9900] + "…";

			int? rating = item.Rating.HasValue ? (int)Math.Round(item.Rating.Value * 2) : null;
			if (rating is { } r && (r < 1 || r > 10)) rating = null;

			if (!reviewerCache.TryGetValue(item.Handle, out var reviewerId))
			{
				var reviewers = scope.ServiceProvider.GetRequiredService<IExternalReviewerService>();
				var reviewer = await reviewers.GetOrCreateAsync("letterboxd", item.Handle, ct);
				reviewerId = reviewer.Id;
				reviewerCache[item.Handle] = reviewerId;
			}

			if (existingRes.IsFailure)
			{
				await uow.ExternalReviewRepository.AddAsync(new ExternalReview
				{
					Id = Guid.NewGuid(),
					MediaId = media.Id,
					Source = "letterboxd",
					ExternalRefId = refId,
					AuthorHandle = item.Handle,
					AuthorUrl = $"https://letterboxd.com/{item.Handle}/",
					ExternalReviewerId = reviewerId,
					Content = content,
					Rating = rating,
					LikeCountOnSource = null,
					SourceUrl = item.Link,
					PublishedAt = item.Published,
					FetchedAt = DateTime.UtcNow,
				});
			}
			else
			{
				var existing = existingRes.Value;
				existing.Content = content;
				existing.Rating = rating;
				existing.ExternalReviewerId = reviewerId;
				existing.FetchedAt = DateTime.UtcNow;
				await uow.ExternalReviewRepository.Update(existing);
			}
		}
		await uow.SaveAsync();
	}

	private static string ResolveStatus(ExternalFetchState? state)
	{
		if (state is null || state.LastFetchedAt is null) return "loading";
		if (state.LastErrorAt is not null && state.LastErrorAt > state.LastFetchedAt) return "error";
		return "ready";
	}

	private static string StripHtml(string html)
	{
		if (string.IsNullOrEmpty(html)) return string.Empty;
		var noTags = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");
		var decoded = System.Net.WebUtility.HtmlDecode(noTags);
		var collapsed = System.Text.RegularExpressions.Regex.Replace(decoded, @"\s+", " ").Trim();
		return collapsed;
	}

	private async Task<string?> ResolveImdbIdAsync(Media media, CancellationToken ct)
	{
		// Media.ExternalApiId looks like "Tmdb:movie:550" or "Tmdb:tv:1399".
		var ext = media.ExternalApiId;
		if (string.IsNullOrEmpty(ext) || !ext.Contains(':')) return null;
		if (!_tmdbEnabled) return null;
		var parts = ext.Split(':', 3);
		if (parts.Length != 3 || !parts[0].Equals("Tmdb", StringComparison.OrdinalIgnoreCase)) return null;
		var tmdbPath = parts[1].Equals("series", StringComparison.OrdinalIgnoreCase) ? "tv" : parts[1].ToLowerInvariant();
		if (tmdbPath != "movie" && tmdbPath != "tv") return null;
		if (string.IsNullOrEmpty(_tmdbKey)) return null;

		var url = $"https://api.themoviedb.org/3/{tmdbPath}/{parts[2]}/external_ids?api_key={_tmdbKey}";
		try
		{
			var http = _httpFactory.CreateClient();
			http.Timeout = TimeSpan.FromSeconds(10);
			var body = await http.GetStringAsync(url, ct);
			using var doc = JsonDocument.Parse(body);
			if (doc.RootElement.TryGetProperty("imdb_id", out var v) && v.ValueKind == JsonValueKind.String)
			{
				var id = v.GetString();
				return string.IsNullOrWhiteSpace(id) ? null : id;
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "TMDB external_ids fetch failed for {Ext}", ext);
		}
		return null;
	}

	private async Task UpsertRatingAsync(IUnitOfWork uow, Guid mediaId, string source, double score, string? raw, int? votes)
	{
		var existing = await uow.ExternalRatingRepository.GetOneAsync(r => r.MediaId == mediaId && r.Source == source);
		if (existing.IsSuccess)
		{
			var e = existing.Value;
			e.Score = score;
			e.RawScore = raw;
			e.VoteCount = votes;
			e.FetchedAt = DateTime.UtcNow;
			await uow.ExternalRatingRepository.Update(e);
		}
		else
		{
			await uow.ExternalRatingRepository.AddAsync(new ExternalRating
			{
				Id = Guid.NewGuid(),
				MediaId = mediaId,
				Source = source,
				Score = score,
				RawScore = raw,
				VoteCount = votes,
				FetchedAt = DateTime.UtcNow,
			});
		}
	}

	private async Task UpsertReviewAsync(IUnitOfWork uow, Guid mediaId, string source, string refId,
		string? authorHandle, string? authorUrl, string content, int? rating, int? likeCount, string? sourceUrl, DateTime? publishedAt)
	{
		var existing = await uow.ExternalReviewRepository.GetOneAsync(r => r.Source == source && r.ExternalRefId == refId);
		if (existing.IsSuccess)
		{
			var e = existing.Value;
			e.MediaId = mediaId;
			e.AuthorHandle = authorHandle;
			e.AuthorUrl = authorUrl;
			e.Content = content;
			e.Rating = rating;
			e.LikeCountOnSource = likeCount;
			e.SourceUrl = sourceUrl;
			e.PublishedAt = publishedAt;
			e.FetchedAt = DateTime.UtcNow;
			await uow.ExternalReviewRepository.Update(e);
		}
		else
		{
			await uow.ExternalReviewRepository.AddAsync(new ExternalReview
			{
				Id = Guid.NewGuid(),
				MediaId = mediaId,
				Source = source,
				ExternalRefId = refId,
				AuthorHandle = authorHandle,
				AuthorUrl = authorUrl,
				Content = content,
				Rating = rating,
				LikeCountOnSource = likeCount,
				SourceUrl = sourceUrl,
				PublishedAt = publishedAt,
				FetchedAt = DateTime.UtcNow,
			});
		}
	}

	private async Task<List<ExternalRating>> LoadRatingsAsync(Guid mediaId)
	{
		var r = await _uow.ExternalRatingRepository.GetAsync(x => x.MediaId == mediaId);
		return r.IsSuccess ? r.Value.ToList() : new List<ExternalRating>();
	}

	private async Task<List<ExternalReview>> LoadReviewsAsync(Guid mediaId)
	{
		// Include the reviewer so the media page can render avatar + display name
		// without an extra round-trip per card.
		var r = await _uow.ExternalReviewRepository.GetAsync(
			x => x.MediaId == mediaId,
			includeProperties: nameof(ExternalReview.ExternalReviewer));
		return r.IsSuccess ? r.Value.OrderByDescending(x => x.LikeCountOnSource ?? 0).ThenByDescending(x => x.PublishedAt ?? DateTime.MinValue).ToList() : new List<ExternalReview>();
	}

	private void QueueBackgroundRefresh(Guid mediaId)
	{
		// Fire-and-forget via dedicated scope. Don't await in the request path.
		_ = Task.Run(async () =>
		{
			try
			{
				using var scope = _scopeFactory.CreateScope();
				var svc = scope.ServiceProvider.GetRequiredService<IExternalContentService>();
				await svc.RefreshAsync(mediaId, CancellationToken.None);
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "Background refresh failed for {Id}", mediaId);
			}
		});
	}
}
