namespace api.DTOs;

public class ExternalReviewerDto
{
	public Guid Id { get; set; }
	public string Source { get; set; } = string.Empty;
	public string Handle { get; set; } = string.Empty;
	public string? DisplayName { get; set; }
	public string? Bio { get; set; }
	public string? AvatarUrl { get; set; }
	public string? SourceProfileUrl { get; set; }
	public DateTime? LastSyncedAt { get; set; }
}

public class ExternalReviewerProfileDto : ExternalReviewerDto
{
	public int ReviewCount { get; set; }
	public double? AverageRating { get; set; }
	public List<ExternalReviewWithMediaDto> RecentReviews { get; set; } = new();
}

/// <summary>
///     ExternalReview enriched with the media row it points at — used by reviewer profile
///     and global external feed so the UI can render "X reviewed Y" cards without N+1 lookups.
/// </summary>
public class ExternalReviewWithMediaDto
{
	public Guid Id { get; set; }
	public Guid MediaId { get; set; }
	public string? MediaTitle { get; set; }
	public int? MediaReleaseYear { get; set; }
	public string? MediaPosterUrl { get; set; }
	public string Source { get; set; } = string.Empty;
	public string? AuthorHandle { get; set; }
	public string? AuthorUrl { get; set; }
	public string Content { get; set; } = string.Empty;
	public int? Rating { get; set; }
	public string? SourceUrl { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime FetchedAt { get; set; }
}

public class ExternalReviewFeedItemDto : ExternalReviewWithMediaDto
{
	public ExternalReviewerDto? Reviewer { get; set; }
}

public class CursorPagedResultDto<T>
{
	public List<T> Items { get; set; } = new();
	public string? NextCursor { get; set; }
}
