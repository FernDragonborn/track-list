namespace api.Services.IServices;

public interface IPublicStatsService
{
	/// <summary>
	///     Returns aggregated, anonymous counts for the About page.
	///     Cached in-memory with a 10-minute TTL — handful of COUNT/AVG queries hit DB only on cache miss.
	/// </summary>
	Task<PublicStatsDto> GetAsync(CancellationToken ct);
}
