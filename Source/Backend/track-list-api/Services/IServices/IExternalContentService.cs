namespace api.Services.IServices;

public interface IExternalContentService
{
	/// <summary>
	///     Fetch cached external content for a media. Triggers a background refresh if missing
	///     or stale (NextFetchDueAt &lt; now). Returns immediately with whatever is cached.
	/// </summary>
	Task<ExternalContentDto> GetForMediaAsync(Guid mediaId, CancellationToken ct);

	/// <summary>
	///     Force-refresh a single media's external content synchronously (used by background service and on-demand).
	///     Updates ExternalFetchState bookkeeping. Returns true on success.
	/// </summary>
	Task<bool> RefreshAsync(Guid mediaId, CancellationToken ct);

	/// <summary>
	///     Returns the next TTL window for a given FetchCount: 1 → 24h, 2 → 3d, 3+ → 7d.
	/// </summary>
	TimeSpan NextTtl(int fetchCount);

	/// <summary>
	///     Batch lookup of cached ratings for many media at once (for catalog cards).
	///     Does NOT trigger refresh — only returns whatever is currently cached.
	/// </summary>
	Task<Dictionary<Guid, List<ExternalRatingDto>>> GetRatingsBatchAsync(IEnumerable<Guid> mediaIds, CancellationToken ct);

	/// <summary>
	///     Batch lookup combining our aggregate rating (avg + count) and external ratings.
	///     Used by catalog cards to render "our" first, externals after.
	/// </summary>
	Task<Dictionary<Guid, MediaRatingsBatchEntryDto>> GetMediaRatingsBatchAsync(IEnumerable<Guid> mediaIds, CancellationToken ct);
}
