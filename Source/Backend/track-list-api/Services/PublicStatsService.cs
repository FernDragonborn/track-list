using api.DbContext;
using api.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class PublicStatsService : IPublicStatsService
{
	private readonly TrackListDbContext _db;
	private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
	// Single-instance cache shared across requests. Cheap: small struct + DateTime.
	private static PublicStatsDto? _cached;
	private static DateTime _cachedAt;
	private static readonly SemaphoreSlim _gate = new(1, 1);

	public PublicStatsService(TrackListDbContext db) { _db = db; }

	public async Task<PublicStatsDto> GetAsync(CancellationToken ct)
	{
		if (_cached is not null && DateTime.UtcNow - _cachedAt < Ttl)
			return _cached;

		await _gate.WaitAsync(ct);
		try
		{
			if (_cached is not null && DateTime.UtcNow - _cachedAt < Ttl)
				return _cached;

			var users = await _db.Users.CountAsync(ct);
			var movies = await _db.Media.CountAsync(m => m.Type == MediaType.Movie, ct);
			var series = await _db.Media.CountAsync(m => m.Type == MediaType.Series, ct);
			var reviews = await _db.Reviews.CountAsync(ct);
			var reviewsWithText = await _db.Reviews
				.CountAsync(r => r.Content != null && r.Content != string.Empty, ct);
			var comments = await _db.Comments.CountAsync(ct);
			var avg = reviews == 0 ? (double?)null
				: await _db.Reviews.AverageAsync(r => (double)r.Rating, ct);

			_cached = new PublicStatsDto
			{
				Users = users,
				Media = movies + series,
				Movies = movies,
				Series = series,
				Reviews = reviews,
				ReviewsWithText = reviewsWithText,
				Comments = comments,
				AvgRating = avg,
				ComputedAt = DateTime.UtcNow,
			};
			_cachedAt = DateTime.UtcNow;
			return _cached;
		}
		finally
		{
			_gate.Release();
		}
	}
}
