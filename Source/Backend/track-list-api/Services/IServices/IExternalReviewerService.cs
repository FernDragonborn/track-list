using api.Models;

namespace api.Services.IServices;

public interface IExternalReviewerService
{
	/// <summary>
	///     Look up an external reviewer by (source, handle); create a row with default
	///     metadata (avatar + profile URL) if missing. Returns the persisted entity.
	/// </summary>
	Task<ExternalReviewer> GetOrCreateAsync(string source, string handle, CancellationToken ct);

	/// <summary>
	///     Single reviewer profile aggregate: metadata + counts + recent reviews.
	///     Returns null when no such reviewer exists.
	/// </summary>
	Task<ExternalReviewerProfile?> GetProfileAsync(string source, string handle, int recentReviewLimit, CancellationToken ct);

	/// <summary>Paginated reviews for one reviewer (cursor = ISO timestamp of the previous tail).</summary>
	Task<(List<ExternalReview> Items, string? NextCursor)> GetReviewsAsync(
		string source, string handle, string? cursor, int limit, CancellationToken ct);

	/// <summary>Global chronological feed across every external reviewer.</summary>
	Task<(List<ExternalReview> Items, string? NextCursor)> GetGlobalFeedAsync(
		string? cursor, int limit, CancellationToken ct);
}

/// <summary>Container DTO returned by GetProfileAsync — kept as a service-level record.</summary>
public record ExternalReviewerProfile(
	ExternalReviewer Reviewer,
	int ReviewCount,
	double? AverageRating,
	List<ExternalReview> RecentReviews);
