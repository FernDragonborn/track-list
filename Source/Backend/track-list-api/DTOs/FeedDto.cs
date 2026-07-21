namespace api.DTOs;

/// <summary>
///     Feed item: review + media context + top comment.
/// </summary>
public record FeedItemDto
{
    public Guid ReviewId { get; init; }
    public Guid MediaId { get; init; }
    public string? MediaExternalId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaPosterUrl { get; init; }

    // Review author
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? ProfilePicUrl { get; init; }

    // Review content
    public int Rating { get; init; }
    public string? Content { get; init; }
    public DateTime CreatedAt { get; init; }

    // Engagement
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public bool IsLikedByMe { get; init; }

    // Top comment (most liked, US-304)
    public FeedCommentDto? TopComment { get; init; }
}

public record FeedCommentDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Content { get; init; }
    public int LikeCount { get; init; }
}
