using static api.DTOs.ResponseTypes;

namespace api.Services.IServices;

public interface IReviewService
{
	Task<Result<ReviewResponseDto>> CreateReviewAsync(Guid mediaId, Guid userId, CreateReviewRequest req);
	Task<Result<PagedResponse<ReviewResponseDto>>> GetReviewsForMediaAsync(Guid mediaId, Guid? currentUserId, int pageNumber, int pageSize);
	Task<Result<PagedResponse<ReviewResponseDto>>> GetReviewsByUserAsync(Guid userId, Guid? currentUserId, int pageNumber, int pageSize);
	Task<Result> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateReviewRequest req);
	Task<Result> DeleteReviewAsync(Guid reviewId, Guid userId, bool isAdmin);

	Task<Result<LikeResponseDto>> ToggleReviewLikeAsync(Guid reviewId, Guid userId);

	Task<Result<CommentResponseDto>> CreateCommentAsync(Guid reviewId, Guid userId, CreateCommentRequest req);
	Task<Result<List<CommentResponseDto>>> GetCommentsForReviewAsync(Guid reviewId, Guid? currentUserId);
	Task<Result> DeleteCommentAsync(Guid commentId, Guid userId, bool isAdmin);
	Task<Result<LikeResponseDto>> ToggleCommentLikeAsync(Guid commentId, Guid userId);
}
