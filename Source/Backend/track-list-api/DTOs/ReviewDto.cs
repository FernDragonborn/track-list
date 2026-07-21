using System.ComponentModel.DataAnnotations;

namespace api.DTOs;

/// <summary>
///     Request DTO for creating a review.
/// </summary>
public record CreateReviewRequest
{
	[Range(1, 10)] public int Rating { get; init; }
	[MaxLength(10000)] public string? Content { get; init; }
}

/// <summary>
///     Request DTO for updating a review.
/// </summary>
public record UpdateReviewRequest
{
	[Range(1, 10)] public int Rating { get; init; }
	[MaxLength(10000)] public string? Content { get; init; }
}

/// <summary>
///     Response DTO for a review.
/// </summary>
public record ReviewResponseDto
{
	public Guid Id { get; init; }
	public Guid MediaId { get; init; }
	public Guid UserId { get; init; }
	public string Username { get; init; } = string.Empty;
	public string? ProfilePicUrl { get; init; }
	public int Rating { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public int LikeCount { get; init; }
	public int CommentCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public string? MediaTitle { get; init; }
	public string? MediaPosterUrl { get; init; }
	public string? MediaExternalApiId { get; init; }
	public bool IsFromFollowing { get; init; }
}
