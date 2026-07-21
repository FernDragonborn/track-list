using System.ComponentModel.DataAnnotations;

namespace api.DTOs;

/// <summary>
///     Request DTO for creating a comment.
/// </summary>
public record CreateCommentRequest
{
	[Required] [MaxLength(10240)] public string Content { get; init; } = string.Empty;
	public Guid? ParentCommentId { get; init; }
}

/// <summary>
///     Response DTO for a comment (with optional nested replies).
/// </summary>
public record CommentResponseDto
{
	public Guid Id { get; init; }
	public Guid ReviewId { get; init; }
	public Guid UserId { get; init; }
	public string Username { get; init; } = string.Empty;
	public string? ProfilePicUrl { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public Guid? ParentCommentId { get; init; }
	public int LikeCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public List<CommentResponseDto> Replies { get; init; } = [];
}
