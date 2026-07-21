using System.Security.Claims;
using api.Identity;
using api.Services.IServices;
using api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api.Controllers;

/// <summary>
///     Controller for reviews, comments, and likes on media items.
/// </summary>
[Route("api/media/{mediaId:guid}/reviews")]
[ApiController]
public class ReviewController(IReviewService reviewService, ITranslationService translationService) : ControllerBase
{
	// ── Reviews ─────────────────────────────────────────────

	/// <summary>
	///     Create a review for a media item (BRL-4: one per user per media).
	/// </summary>
	[HttpPost]
	[Authorize]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateReview(Guid mediaId, [FromBody] CreateReviewRequest request)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var result = await reviewService.CreateReviewAsync(mediaId, userId.Value, request);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Get paginated reviews for a media item.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetReviews(Guid mediaId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
	{
		var userId = GetUserId(); // optional — may be null for guests

		var result = await reviewService.GetReviewsForMediaAsync(mediaId, userId, pageNumber, pageSize);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Update own review.
	/// </summary>
	[HttpPut("{reviewId:guid}")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateReview(Guid mediaId, Guid reviewId, [FromBody] UpdateReviewRequest request)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var result = await reviewService.UpdateReviewAsync(reviewId, userId.Value, request);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Delete a review (author or admin).
	/// </summary>
	[HttpDelete("{reviewId:guid}")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> DeleteReview(Guid mediaId, Guid reviewId)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var isAdmin = User.IsInRole(IdentityData.ClaimAdmin.ToString())
		              || User.IsInRole(IdentityData.ClaimModerator.ToString());
		var result = await reviewService.DeleteReviewAsync(reviewId, userId.Value, isAdmin);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return NoContent();
	}

	// ── Review Likes ────────────────────────────────────────

	/// <summary>
	///     Toggle like on a review (like if not liked, unlike if liked).
	/// </summary>
	[HttpPost("{reviewId:guid}/like")]
	[Authorize]
	[EnableRateLimiting("write")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ToggleReviewLike(Guid mediaId, Guid reviewId)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var result = await reviewService.ToggleReviewLikeAsync(reviewId, userId.Value);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	// ── Comments ────────────────────────────────────────────

	/// <summary>
	///     Add a comment to a review. Set ParentCommentId for replies (max 1 level deep).
	/// </summary>
	[HttpPost("{reviewId:guid}/comments")]
	[Authorize]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateComment(Guid mediaId, Guid reviewId, [FromBody] CreateCommentRequest request)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var result = await reviewService.CreateCommentAsync(reviewId, userId.Value, request);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Get all comments for a review (threaded: level 0 with nested replies).
	/// </summary>
	[HttpGet("{reviewId:guid}/comments")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComments(Guid mediaId, Guid reviewId)
	{
		var userId = GetUserId();

		var result = await reviewService.GetCommentsForReviewAsync(reviewId, userId);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Delete a comment (author or admin).
	/// </summary>
	[HttpDelete("{reviewId:guid}/comments/{commentId:guid}")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> DeleteComment(Guid mediaId, Guid reviewId, Guid commentId)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var isAdmin = User.IsInRole(IdentityData.ClaimAdmin.ToString())
		              || User.IsInRole(IdentityData.ClaimModerator.ToString());
		var result = await reviewService.DeleteCommentAsync(commentId, userId.Value, isAdmin);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return NoContent();
	}

	// ── Comment Likes ───────────────────────────────────────

	/// <summary>
	///     Toggle like on a comment.
	/// </summary>
	[HttpPost("{reviewId:guid}/comments/{commentId:guid}/like")]
	[Authorize]
	[EnableRateLimiting("write")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ToggleCommentLike(Guid mediaId, Guid reviewId, Guid commentId)
	{
		var userId = GetUserId();
		if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var result = await reviewService.ToggleCommentLikeAsync(commentId, userId.Value);
		if (result.IsFailure) return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	// ── Translation ─────────────────────────────────────────

	/// <summary>
	///     On-demand translation for a single review body.
	/// </summary>
	[HttpGet("{reviewId:guid}/translate")]
	[EnableRateLimiting("expensive")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> TranslateReview(Guid mediaId, Guid reviewId, [FromQuery] string lang, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(lang))
			return BadRequest(new { error = "lang query param required" });

		var result = await translationService.TranslateReviewAsync(reviewId, lang, ct);
		if (result.IsFailure) return NotFound(new { error = result.Error });

		return Ok(new { data = new { translation = result.Value, lang = lang.ToLowerInvariant() } });
	}

	/// <summary>
	///     On-demand translation for a single comment body.
	/// </summary>
	[HttpGet("{reviewId:guid}/comments/{commentId:guid}/translate")]
	[EnableRateLimiting("expensive")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> TranslateComment(Guid mediaId, Guid reviewId, Guid commentId, [FromQuery] string lang, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(lang))
			return BadRequest(new { error = "lang query param required" });

		var result = await translationService.TranslateCommentAsync(commentId, lang, ct);
		if (result.IsFailure) return NotFound(new { error = result.Error });

		return Ok(new { data = new { translation = result.Value, lang = lang.ToLowerInvariant() } });
	}

	// ── Helper ──────────────────────────────────────────────

	private Guid? GetUserId()
	{
		var idStr = User.FindFirstValue("id")
		            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

		return Guid.TryParse(idStr, out var guid) ? guid : null;
	}
}
