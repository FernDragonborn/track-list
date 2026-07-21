using api.DTOs;
using api.Services.IServices;
using static api.DTOs.ResponseTypes;

namespace api.Services;

public class FeedService(IUnitOfWork unitOfWork) : IFeedService
{
    // Global feed always hides reviews shorter than this; personal feed can opt in via showShort=false.
    private const int MinContentLen = 100;

    public async Task<Result<PagedResponse<FeedItemDto>>> GetPersonalFeedAsync(
        Guid userId, int pageNumber, int pageSize, bool showShort = true)
    {
        // Get IDs of users this user follows
        var followsRes = await unitOfWork.FollowRepository.GetAsync(f => f.FollowerId == userId);
        if (followsRes.IsFailure)
            return Result.Fail<PagedResponse<FeedItemDto>>(followsRes.Error);

        var followedIds = followsRes.Value.Select(f => f.FollowingId).ToHashSet();

        if (followedIds.Count == 0)
            return Result.Ok(new PagedResponse<FeedItemDto>([], 0, pageNumber, pageSize));

        // Get paged reviews from followed users
        var reviewsRes = await unitOfWork.ReviewRepository.GetPagedAsync(
            r => followedIds.Contains(r.UserId)
                 && (showShort || (r.Content != null && r.Content.Length >= MinContentLen)),
            q => q.OrderByDescending(r => r.CreatedAt),
            pageNumber,
            pageSize);

        if (reviewsRes.IsFailure)
            return Result.Fail<PagedResponse<FeedItemDto>>(reviewsRes.Error);

        var (reviews, totalCount) = reviewsRes.Value;
        var items = await MapToFeedItems(reviews, userId);

        return Result.Ok(new PagedResponse<FeedItemDto>(items, totalCount, pageNumber, pageSize));
    }

    public async Task<Result<PagedResponse<FeedItemDto>>> GetGlobalFeedAsync(
        Guid? currentUserId, int pageNumber, int pageSize)
    {
        var reviewsRes = await unitOfWork.ReviewRepository.GetPagedAsync(
            r => r.Content != null && r.Content.Length >= MinContentLen,
            q => q.OrderByDescending(r => r.CreatedAt),
            pageNumber,
            pageSize);

        if (reviewsRes.IsFailure)
            return Result.Fail<PagedResponse<FeedItemDto>>(reviewsRes.Error);

        var (reviews, totalCount) = reviewsRes.Value;
        var items = await MapToFeedItems(reviews, currentUserId);

        return Result.Ok(new PagedResponse<FeedItemDto>(items, totalCount, pageNumber, pageSize));
    }

    public async Task<Result<PagedResponse<FeedItemDto>>> GetMyReviewsAsync(
        Guid userId, int pageNumber, int pageSize, string? sortBy = null)
    {
        Func<IQueryable<Review>, IOrderedQueryable<Review>> orderBy = sortBy switch
        {
            "oldest"      => q => q.OrderBy(r => r.CreatedAt),
            "rating_desc" => q => q.OrderByDescending(r => r.Rating),
            "rating_asc"  => q => q.OrderBy(r => r.Rating),
            _             => q => q.OrderByDescending(r => r.CreatedAt)
        };

        var reviewsRes = await unitOfWork.ReviewRepository.GetPagedAsync(
            r => r.UserId == userId,
            orderBy,
            pageNumber,
            pageSize);

        if (reviewsRes.IsFailure)
            return Result.Fail<PagedResponse<FeedItemDto>>(reviewsRes.Error);

        var (reviews, totalCount) = reviewsRes.Value;
        var items = await MapToFeedItems(reviews, userId);

        return Result.Ok(new PagedResponse<FeedItemDto>(items, totalCount, pageNumber, pageSize));
    }

    private async Task<List<FeedItemDto>> MapToFeedItems(List<Review> reviews, Guid? currentUserId)
    {
        var items = new List<FeedItemDto>();

        foreach (var review in reviews)
        {
            // User info
            var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == review.UserId);
            var username = userRes.IsSuccess ? (userRes.Value.Username ?? string.Empty) : string.Empty;
            var profilePic = userRes.IsSuccess ? userRes.Value.ProfilePicUrl : null;

            // Media info
            var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == review.MediaId);
            string? mediaTitle = null;
            string? mediaPoster = null;
            string? mediaExternalId = null;
            if (mediaRes.IsSuccess)
            {
                mediaPoster = mediaRes.Value.PosterUrl;
                mediaExternalId = mediaRes.Value.ExternalApiId;
                var translationRes = await unitOfWork.MediaTranslationRepository.GetOneAsync(
                    t => t.MediaId == review.MediaId);
                mediaTitle = translationRes.IsSuccess ? translationRes.Value.Title : null;
            }

            // Likes
            var likesRes = await unitOfWork.ReviewLikeRepository.GetAsync(l => l.ReviewId == review.Id);
            var likeCount = likesRes.IsSuccess ? likesRes.Value.Count : 0;
            var isLikedByMe = currentUserId.HasValue && likesRes.IsSuccess
                              && likesRes.Value.Any(l => l.UserId == currentUserId.Value);

            // Comments count
            var commentsRes = await unitOfWork.CommentRepository.GetAsync(c => c.ReviewId == review.Id);
            var commentCount = commentsRes.IsSuccess ? commentsRes.Value.Count : 0;

            // Top comment (most liked level-0 comment)
            FeedCommentDto? topComment = null;
            if (commentsRes.IsSuccess && commentsRes.Value.Count > 0)
            {
                var topLevelComments = commentsRes.Value.Where(c => c.ParentCommentId == null).ToList();
                if (topLevelComments.Count > 0)
                {
                    // Find the one with most likes
                    Comment? bestComment = null;
                    var bestLikeCount = -1;

                    foreach (var comment in topLevelComments)
                    {
                        var cLikesRes = await unitOfWork.CommentLikeRepository.GetAsync(
                            l => l.CommentId == comment.Id);
                        var cLikeCount = cLikesRes.IsSuccess ? cLikesRes.Value.Count : 0;
                        if (cLikeCount > bestLikeCount)
                        {
                            bestLikeCount = cLikeCount;
                            bestComment = comment;
                        }
                    }

                    if (bestComment is not null)
                    {
                        var commentUserRes = await unitOfWork.UserRepository.GetOneAsync(
                            u => u.Id == bestComment.UserId);
                        topComment = new FeedCommentDto
                        {
                            Id = bestComment.Id,
                            Username = commentUserRes.IsSuccess
                                ? (commentUserRes.Value.Username ?? string.Empty)
                                : string.Empty,
                            Content = bestComment.Content,
                            LikeCount = bestLikeCount
                        };
                    }
                }
            }

            items.Add(new FeedItemDto
            {
                ReviewId = review.Id,
                MediaId = review.MediaId,
                MediaExternalId = mediaExternalId,
                MediaTitle = mediaTitle,
                MediaPosterUrl = mediaPoster,
                UserId = review.UserId,
                Username = username,
                ProfilePicUrl = profilePic,
                Rating = review.Rating,
                Content = review.Content,
                CreatedAt = review.CreatedAt,
                LikeCount = likeCount,
                CommentCount = commentCount,
                IsLikedByMe = isLikedByMe,
                TopComment = topComment
            });
        }

        return items;
    }
}
