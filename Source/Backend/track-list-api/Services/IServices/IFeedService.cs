using api.DTOs;
using static api.DTOs.ResponseTypes;

namespace api.Services.IServices;

public interface IFeedService
{
    /// <summary>
    ///     Personal feed: reviews from users the current user follows (US-301).
    ///     showShort=true (default) includes all reviews; false filters out reviews
    ///     shorter than the 100-char minimum used by the global feed.
    /// </summary>
    Task<Result<PagedResponse<FeedItemDto>>> GetPersonalFeedAsync(Guid userId, int pageNumber, int pageSize, bool showShort = true);

    /// <summary>
    ///     Global feed: all reviews, newest first (US-302).
    ///     Always hides reviews with fewer than 100 chars of content (low-signal noise).
    /// </summary>
    Task<Result<PagedResponse<FeedItemDto>>> GetGlobalFeedAsync(Guid? currentUserId, int pageNumber, int pageSize);

    /// <summary>
    ///     My reviews: all reviews by the current user.
    ///     sortBy: newest (default) | oldest | rating_desc | rating_asc
    /// </summary>
    Task<Result<PagedResponse<FeedItemDto>>> GetMyReviewsAsync(Guid userId, int pageNumber, int pageSize, string? sortBy = null);
}
