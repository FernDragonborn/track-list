using System.Globalization;
using api.DbContext;
using api.Models;
using api.Repository.IReposotory;
using api.Services.External;
using api.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class ExternalReviewerService : IExternalReviewerService
{
	private readonly TrackListDbContext _db;
	private readonly LetterboxdRssClient _letterboxd;

	public ExternalReviewerService(TrackListDbContext db, LetterboxdRssClient letterboxd)
	{
		_db = db;
		_letterboxd = letterboxd;
	}

	public async Task<ExternalReviewer> GetOrCreateAsync(string source, string handle, CancellationToken ct)
	{
		var normHandle = handle.Trim();
		var normSource = source.Trim().ToLowerInvariant();

		var existing = await _db.ExternalReviewers
			.FirstOrDefaultAsync(r => r.Source == normSource && r.Handle == normHandle, ct);
		if (existing is not null) return existing;

		// Scrape the real avatar from the Letterboxd profile page (og:image meta).
		// Avoids mixing externals with our DiceBear-generated user avatars. Null on
		// failure — UI falls back to a source-icon placeholder.
		string? avatar = null;
		if (normSource == "letterboxd")
		{
			avatar = await _letterboxd.TryFetchAvatarUrlAsync(normHandle, ct);
		}

		var reviewer = new ExternalReviewer
		{
			Id = Guid.NewGuid(),
			Source = normSource,
			Handle = normHandle,
			AvatarUrl = avatar,
			SourceProfileUrl = normSource == "letterboxd"
				? $"https://letterboxd.com/{normHandle}/"
				: null,
			LastSyncedAt = avatar is null ? null : DateTime.UtcNow,
		};

		try
		{
			_db.ExternalReviewers.Add(reviewer);
			await _db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException)
		{
			// Concurrent insert race — re-read.
			_db.Entry(reviewer).State = EntityState.Detached;
			existing = await _db.ExternalReviewers
				.FirstOrDefaultAsync(r => r.Source == normSource && r.Handle == normHandle, ct);
			if (existing is not null) return existing;
			throw;
		}

		return reviewer;
	}

	public async Task<ExternalReviewerProfile?> GetProfileAsync(string source, string handle, int recentReviewLimit, CancellationToken ct)
	{
		var normHandle = handle.Trim();
		var normSource = source.Trim().ToLowerInvariant();

		var reviewer = await _db.ExternalReviewers
			.FirstOrDefaultAsync(r => r.Source == normSource && r.Handle == normHandle, ct);
		if (reviewer is null) return null;

		// Note: prefer (Source, AuthorHandle) over FK here because legacy rows existed before
		// the FK column was added; the bridge backfills them but old reviews in mid-migration
		// can have NULL FK, which the FK-based query would miss.
		var count = await _db.ExternalReviews
			.Where(r => r.Source == normSource && r.AuthorHandle == normHandle)
			.CountAsync(ct);
		double? avg = null;
		if (count > 0)
		{
			avg = await _db.ExternalReviews
				.Where(r => r.Source == normSource && r.AuthorHandle == normHandle && r.Rating != null)
				.AverageAsync(r => (double?)r.Rating, ct);
		}

		var recent = await _db.ExternalReviews
			.Include(r => r.Media!).ThenInclude(m => m.Translations)
			.Where(r => r.Source == normSource && r.AuthorHandle == normHandle)
			.OrderByDescending(r => r.PublishedAt ?? r.FetchedAt)
			.Take(recentReviewLimit)
			.ToListAsync(ct);

		return new ExternalReviewerProfile(reviewer, count, avg, recent);
	}

	public async Task<(List<ExternalReview> Items, string? NextCursor)> GetReviewsAsync(
		string source, string handle, string? cursor, int limit, CancellationToken ct)
	{
		var normHandle = handle.Trim();
		var normSource = source.Trim().ToLowerInvariant();
		limit = Math.Clamp(limit, 1, 100);

		var reviewer = await _db.ExternalReviewers
			.FirstOrDefaultAsync(r => r.Source == normSource && r.Handle == normHandle, ct);
		if (reviewer is null) return (new List<ExternalReview>(), null);

		DateTime? cursorTs = null;
		if (!string.IsNullOrEmpty(cursor) && DateTime.TryParse(cursor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
			cursorTs = parsed;

		var q = _db.ExternalReviews
			.Include(r => r.Media!).ThenInclude(m => m.Translations)
			.Where(r => r.Source == normSource && r.AuthorHandle == normHandle);
		if (cursorTs is not null)
			q = q.Where(r => (r.PublishedAt ?? r.FetchedAt) < cursorTs);

		var items = await q
			.OrderByDescending(r => r.PublishedAt ?? r.FetchedAt)
			.Take(limit + 1)
			.ToListAsync(ct);

		string? nextCursor = null;
		if (items.Count > limit)
		{
			var tail = items[limit - 1];
			nextCursor = (tail.PublishedAt ?? tail.FetchedAt).ToString("O");
			items.RemoveAt(items.Count - 1);
		}
		return (items, nextCursor);
	}

	public async Task<(List<ExternalReview> Items, string? NextCursor)> GetGlobalFeedAsync(
		string? cursor, int limit, CancellationToken ct)
	{
		limit = Math.Clamp(limit, 1, 100);
		DateTime? cursorTs = null;
		if (!string.IsNullOrEmpty(cursor) && DateTime.TryParse(cursor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
			cursorTs = parsed;

		var q = _db.ExternalReviews
			.Include(r => r.Media!).ThenInclude(m => m.Translations)
			.Include(r => r.ExternalReviewer)
			.Where(r => r.ExternalReviewerId != null);
		if (cursorTs is not null)
			q = q.Where(r => (r.PublishedAt ?? r.FetchedAt) < cursorTs);

		var items = await q
			.OrderByDescending(r => r.PublishedAt ?? r.FetchedAt)
			.Take(limit + 1)
			.ToListAsync(ct);

		string? nextCursor = null;
		if (items.Count > limit)
		{
			var tail = items[limit - 1];
			nextCursor = (tail.PublishedAt ?? tail.FetchedAt).ToString("O");
			items.RemoveAt(items.Count - 1);
		}
		return (items, nextCursor);
	}
}
