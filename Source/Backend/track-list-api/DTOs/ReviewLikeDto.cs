namespace api.DTOs;

/// <summary>
///     Response DTO for a like toggle result.
/// </summary>
public record LikeResponseDto
{
	public bool IsLiked { get; init; }
	public int LikeCount { get; init; }
}
