using api.Services.IServices;
using static api.DTOs.ResponseTypes;

namespace api.Services;

public class ReviewService(IUnitOfWork unitOfWork) : IReviewService
{
	// ── Reviews ─────────────────────────────��────────────────

	public async Task<Result<ReviewResponseDto>> CreateReviewAsync(Guid mediaId, Guid userId, CreateReviewRequest req)
	{
		var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == mediaId);
		if (mediaRes.IsFailure)
			return Result.Fail<ReviewResponseDto>("Media not found");

		var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userId);
		if (userRes.IsFailure)
			return Result.Fail<ReviewResponseDto>("User not found.");
		var username = userRes.Value.Username;
		var profilePic = userRes.Value.ProfilePicUrl;

		// BRL-4: check including soft-deleted to avoid unique constraint violation
		var any = await unitOfWork.ReviewRepository.FindIncludingDeletedAsync(userId, mediaId);

		if (any is not null && any.DeletedAt == null)
			return Result.Fail<ReviewResponseDto>("You already have a review for this media");

		Review review;
		if (any is not null)
		{
			// Reactivate soft-deleted review
			any.DeletedAt = null;
			any.Rating = req.Rating;
			any.Content = req.Content;
			any.UpdatedAt = DateTime.UtcNow;
			await unitOfWork.ReviewRepository.Update(any);
			review = any;
		}
		else
		{
			var addRes = await unitOfWork.ReviewRepository.AddAsync(new Review
			{
				MediaId = mediaId,
				UserId = userId,
				Rating = req.Rating,
				Content = req.Content
			});
			if (addRes.IsFailure)
				return Result.Fail<ReviewResponseDto>(addRes.Error);
			review = addRes.Value;
		}

		await unitOfWork.SaveAsync();
		return Result.Ok(MapReviewToDto(review, username, profilePic, 0, 0, false));
	}

	public async Task<Result<PagedResponse<ReviewResponseDto>>> GetReviewsForMediaAsync(
		Guid mediaId, Guid? currentUserId, int pageNumber, int pageSize)
	{
		var pagedRes = await unitOfWork.ReviewRepository.GetPagedAsync(
			r => r.MediaId == mediaId,
			q => q.OrderByDescending(r => r.CreatedAt),
			pageNumber,
			pageSize);

		if (pagedRes.IsFailure)
			return Result.Fail<PagedResponse<ReviewResponseDto>>(pagedRes.Error);

		var (reviews, totalCount) = pagedRes.Value;
		var dtos = new List<ReviewResponseDto>();

		// Build "I'm following" set once per request (cheap; max ~hundreds of follows per user).
		var followingIds = new HashSet<Guid>();
		if (currentUserId.HasValue)
		{
			var followsRes = await unitOfWork.FollowRepository.GetAsync(f => f.FollowerId == currentUserId.Value);
			if (followsRes.IsSuccess)
				foreach (var f in followsRes.Value) followingIds.Add(f.FollowingId);
		}

		foreach (var review in reviews)
		{
			var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == review.UserId);
			var username = userRes.IsSuccess ? userRes.Value.Username : string.Empty;
			var profilePic = userRes.IsSuccess ? userRes.Value.ProfilePicUrl : null;

			var likesRes = await unitOfWork.ReviewLikeRepository.GetAsync(l => l.ReviewId == review.Id);
			var likeCount = likesRes.IsSuccess ? likesRes.Value.Count : 0;

			var commentsRes = await unitOfWork.CommentRepository.GetAsync(c => c.ReviewId == review.Id);
			var commentCount = commentsRes.IsSuccess ? commentsRes.Value.Count : 0;

			var isLikedByMe = false;
			if (currentUserId.HasValue && likesRes.IsSuccess)
				isLikedByMe = likesRes.Value.Any(l => l.UserId == currentUserId.Value);

			var isFromFollowing = followingIds.Contains(review.UserId);

			dtos.Add(MapReviewToDto(review, username, profilePic, likeCount, commentCount, isLikedByMe, isFromFollowing));
		}

		return Result.Ok(new PagedResponse<ReviewResponseDto>(dtos, totalCount, pageNumber, pageSize));
	}

	public async Task<Result<PagedResponse<ReviewResponseDto>>> GetReviewsByUserAsync(
		Guid userId, Guid? currentUserId, int pageNumber, int pageSize)
	{
		var pagedRes = await unitOfWork.ReviewRepository.GetPagedAsync(
			r => r.UserId == userId,
			q => q.OrderByDescending(r => r.CreatedAt),
			pageNumber,
			pageSize);

		if (pagedRes.IsFailure)
			return Result.Fail<PagedResponse<ReviewResponseDto>>(pagedRes.Error);

		var (reviews, totalCount) = pagedRes.Value;
		var dtos = new List<ReviewResponseDto>();

		var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userId);
		var username = userRes.IsSuccess ? userRes.Value.Username : string.Empty;
		var profilePic = userRes.IsSuccess ? userRes.Value.ProfilePicUrl : null;

		foreach (var review in reviews)
		{
			var likesRes = await unitOfWork.ReviewLikeRepository.GetAsync(l => l.ReviewId == review.Id);
			var likeCount = likesRes.IsSuccess ? likesRes.Value.Count : 0;

			var commentsRes = await unitOfWork.CommentRepository.GetAsync(c => c.ReviewId == review.Id);
			var commentCount = commentsRes.IsSuccess ? commentsRes.Value.Count : 0;

			var isLikedByMe = false;
			if (currentUserId.HasValue && likesRes.IsSuccess)
				isLikedByMe = likesRes.Value.Any(l => l.UserId == currentUserId.Value);

			var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(
				m => m.Id == review.MediaId, "Translations");
			string? mediaTitle = null;
			string? mediaPoster = null;
			string? mediaExternalApiId = null;
			if (mediaRes.IsSuccess)
			{
				mediaPoster = mediaRes.Value.PosterUrl;
				mediaExternalApiId = mediaRes.Value.ExternalApiId;
				var approved = mediaRes.Value.Translations
					.Where(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)
					.ToList();
				mediaTitle = approved.FirstOrDefault(t => t.LanguageCode == "uk")?.Title
					?? approved.FirstOrDefault(t => t.LanguageCode == "en")?.Title
					?? approved.FirstOrDefault()?.Title;
			}

			var dto = MapReviewToDto(review, username, profilePic, likeCount, commentCount, isLikedByMe);
			dtos.Add(dto with
			{
				MediaTitle = mediaTitle,
				MediaPosterUrl = mediaPoster,
				MediaExternalApiId = mediaExternalApiId
			});
		}

		return Result.Ok(new PagedResponse<ReviewResponseDto>(dtos, totalCount, pageNumber, pageSize));
	}

	public async Task<Result> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateReviewRequest req)
	{
		var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == reviewId);
		if (reviewRes.IsFailure)
			return Result.Fail("Review not found");

		if (reviewRes.Value.UserId != userId)
			return Result.Fail("You can only edit your own review");

		reviewRes.Value.Rating = req.Rating;
		reviewRes.Value.Content = req.Content;

		await unitOfWork.ReviewRepository.Update(reviewRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> DeleteReviewAsync(Guid reviewId, Guid userId, bool isAdmin)
	{
		var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == reviewId);
		if (reviewRes.IsFailure)
			return Result.Fail("Review not found");

		if (reviewRes.Value.UserId != userId && !isAdmin)
			return Result.Fail("You can only delete your own review");

		await unitOfWork.ReviewRepository.Remove(reviewRes.Value);
		await ResolveReportsForTargetAsync(reviewId, userId);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	// ── Review Likes ────────────────────────────────────────

	public async Task<Result<LikeResponseDto>> ToggleReviewLikeAsync(Guid reviewId, Guid userId)
	{
		var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == reviewId);
		if (reviewRes.IsFailure)
			return Result.Fail<LikeResponseDto>("Review not found");

		var existingRes = await unitOfWork.ReviewLikeRepository
			.GetOneAsync(l => l.ReviewId == reviewId && l.UserId == userId);

		if (existingRes.IsSuccess)
		{
			await unitOfWork.ReviewLikeRepository.Remove(existingRes.Value);
		}
		else
		{
			var like = new ReviewLike { ReviewId = reviewId, UserId = userId };
			await unitOfWork.ReviewLikeRepository.AddAsync(like);
		}

		await unitOfWork.SaveAsync();

		var allLikesRes = await unitOfWork.ReviewLikeRepository.GetAsync(l => l.ReviewId == reviewId);
		var likeCount = allLikesRes.IsSuccess ? allLikesRes.Value.Count : 0;
		var isLiked = !existingRes.IsSuccess; // toggled

		return Result.Ok(new LikeResponseDto { IsLiked = isLiked, LikeCount = likeCount });
	}

	// ── Comments ────────────────────────────────────────────

	public async Task<Result<CommentResponseDto>> CreateCommentAsync(Guid reviewId, Guid userId, CreateCommentRequest req)
	{
		var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == reviewId);
		if (reviewRes.IsFailure)
			return Result.Fail<CommentResponseDto>("Review not found");

		var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userId);
		if (userRes.IsFailure)
			return Result.Fail<CommentResponseDto>("User not found.");

		// Enforce max nesting: level 1 only
		if (req.ParentCommentId.HasValue)
		{
			var parentRes = await unitOfWork.CommentRepository.GetOneAsync(c => c.Id == req.ParentCommentId.Value);
			if (parentRes.IsFailure)
				return Result.Fail<CommentResponseDto>("Parent comment not found");

			// If parent already has a parent, it's level 1 — can't reply to it
			if (parentRes.Value.ParentCommentId.HasValue)
				return Result.Fail<CommentResponseDto>("Replies are only allowed on top-level comments (max nesting level is 1)");
		}

		var comment = new Comment
		{
			ReviewId = reviewId,
			UserId = userId,
			Content = req.Content,
			ParentCommentId = req.ParentCommentId
		};

		var addRes = await unitOfWork.CommentRepository.AddAsync(comment);
		if (addRes.IsFailure)
			return Result.Fail<CommentResponseDto>(addRes.Error);

		await unitOfWork.SaveAsync();
		return Result.Ok(MapCommentToDto(addRes.Value, userRes.Value.Username, userRes.Value.ProfilePicUrl, 0, false, []));
	}

	public async Task<Result<List<CommentResponseDto>>> GetCommentsForReviewAsync(Guid reviewId, Guid? currentUserId)
	{
		var allCommentsRes = await unitOfWork.CommentRepository.GetAsync(c => c.ReviewId == reviewId);
		if (allCommentsRes.IsFailure)
			return Result.Fail<List<CommentResponseDto>>(allCommentsRes.Error);

		var comments = allCommentsRes.Value;

		// Fetch all likes for these comments in one call
		var commentIds = comments.Select(c => c.Id).ToHashSet();
		var allLikesRes = await unitOfWork.CommentLikeRepository
			.GetAsync(l => commentIds.Contains(l.CommentId));
		var allLikes = allLikesRes.IsSuccess ? allLikesRes.Value : [];

		// Fetch all users involved
		var userIds = comments.Select(c => c.UserId).Distinct().ToList();
		var usersRes = await unitOfWork.UserRepository.GetAsync(u => userIds.Contains(u.Id));
		var usersMap = usersRes.IsSuccess
			? usersRes.Value.ToDictionary(u => u.Id)
			: new Dictionary<Guid, User>();

		// Build threaded structure
		var topLevel = comments.Where(c => c.ParentCommentId == null).OrderBy(c => c.CreatedAt).ToList();
		var repliesMap = comments
			.Where(c => c.ParentCommentId != null)
			.GroupBy(c => c.ParentCommentId!.Value)
			.ToDictionary(g => g.Key, g => g.OrderBy(c => c.CreatedAt).ToList());

		var result = topLevel.Select(c => BuildCommentDto(c, repliesMap, usersMap, allLikes, currentUserId)).ToList();
		return Result.Ok(result);
	}

	public async Task<Result> DeleteCommentAsync(Guid commentId, Guid userId, bool isAdmin)
	{
		var commentRes = await unitOfWork.CommentRepository.GetOneAsync(c => c.Id == commentId);
		if (commentRes.IsFailure)
			return Result.Fail("Comment not found");

		if (commentRes.Value.UserId != userId && !isAdmin)
			return Result.Fail("You can only delete your own comment");

		await unitOfWork.CommentRepository.Remove(commentRes.Value);
		await ResolveReportsForTargetAsync(commentId, userId);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	private async Task ResolveReportsForTargetAsync(Guid targetId, Guid moderatorId)
	{
		var reportsRes = await unitOfWork.ReportRepository.GetAsync(
			r => r.TargetId == targetId && r.Status == ReportStatus.Pending);
		if (reportsRes.IsFailure) return;

		foreach (var report in reportsRes.Value)
		{
			report.Status = ReportStatus.ResolvedDeleted;
			report.ProcessedByUserId = moderatorId;
			await unitOfWork.ReportRepository.Update(report);
		}
	}

	// ── Comment Likes ───────────────────────────────────────

	public async Task<Result<LikeResponseDto>> ToggleCommentLikeAsync(Guid commentId, Guid userId)
	{
		var commentRes = await unitOfWork.CommentRepository.GetOneAsync(c => c.Id == commentId);
		if (commentRes.IsFailure)
			return Result.Fail<LikeResponseDto>("Comment not found");

		var existingRes = await unitOfWork.CommentLikeRepository
			.GetOneAsync(l => l.CommentId == commentId && l.UserId == userId);

		if (existingRes.IsSuccess)
		{
			await unitOfWork.CommentLikeRepository.Remove(existingRes.Value);
		}
		else
		{
			var like = new CommentLike { CommentId = commentId, UserId = userId };
			await unitOfWork.CommentLikeRepository.AddAsync(like);
		}

		await unitOfWork.SaveAsync();

		var allLikesRes = await unitOfWork.CommentLikeRepository.GetAsync(l => l.CommentId == commentId);
		var likeCount = allLikesRes.IsSuccess ? allLikesRes.Value.Count : 0;
		var isLiked = !existingRes.IsSuccess;

		return Result.Ok(new LikeResponseDto { IsLiked = isLiked, LikeCount = likeCount });
	}

	// ── Mapping helpers ─────────────────────────────────────

	private static ReviewResponseDto MapReviewToDto(
		Review review, string username, string? profilePic, int likeCount, int commentCount, bool isLikedByMe, bool isFromFollowing = false) =>
		new()
		{
			Id = review.Id,
			MediaId = review.MediaId,
			UserId = review.UserId,
			Username = username,
			ProfilePicUrl = profilePic,
			Rating = review.Rating,
			Content = review.Content,
			CreatedAt = review.CreatedAt,
			LikeCount = likeCount,
			CommentCount = commentCount,
			IsLikedByMe = isLikedByMe,
			IsFromFollowing = isFromFollowing
		};

	private static CommentResponseDto MapCommentToDto(
		Comment comment, string username, string? profilePic, int likeCount, bool isLikedByMe, List<CommentResponseDto> replies) =>
		new()
		{
			Id = comment.Id,
			ReviewId = comment.ReviewId,
			UserId = comment.UserId,
			Username = username,
			ProfilePicUrl = profilePic,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
			ParentCommentId = comment.ParentCommentId,
			LikeCount = likeCount,
			IsLikedByMe = isLikedByMe,
			Replies = replies
		};

	private CommentResponseDto BuildCommentDto(
		Comment comment,
		Dictionary<Guid, List<Comment>> repliesMap,
		Dictionary<Guid, User> usersMap,
		List<CommentLike> allLikes,
		Guid? currentUserId)
	{
		var username = usersMap.TryGetValue(comment.UserId, out var user) ? user.Username : string.Empty;
		var profilePic = user?.ProfilePicUrl;
		var commentLikes = allLikes.Where(l => l.CommentId == comment.Id).ToList();
		var likeCount = commentLikes.Count;
		var isLikedByMe = currentUserId.HasValue && commentLikes.Any(l => l.UserId == currentUserId.Value);

		var replies = repliesMap.TryGetValue(comment.Id, out var childComments)
			? childComments.Select(c => BuildCommentDto(c, repliesMap, usersMap, allLikes, currentUserId)).ToList()
			: [];

		return MapCommentToDto(comment, username, profilePic, likeCount, isLikedByMe, replies);
	}
}
