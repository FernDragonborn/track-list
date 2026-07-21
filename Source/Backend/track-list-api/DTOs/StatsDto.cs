namespace api.DTOs;

public record PlatformStatsDto
{
    public int TotalUsers { get; init; }
    public int TotalMedia { get; init; }
    public int TotalReviews { get; init; }
    public int TotalComments { get; init; }
    public int TotalCollections { get; init; }
    public int TotalReports { get; init; }
    public int PendingReports { get; init; }
    public int PendingTranslations { get; init; }
    public int NewUsersInPeriod { get; init; }
    public int NewReviewsInPeriod { get; init; }
    public TrackingDistributionDto TrackingDistribution { get; init; } = new(0, 0, 0, 0);
    public DateTime StatsFrom { get; init; }
    public DateTime StatsTo { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public record TrackingDistributionDto(int Planned, int Watching, int Completed, int Dropped);

public record AdminMediaItem
{
    public Guid Id { get; init; }
    public string? ExternalApiId { get; init; }
    public string Type { get; init; } = "";
    public int? ReleaseYear { get; init; }
    public int TranslationCount { get; init; }
    public List<AdminTranslationItem> Translations { get; init; } = [];
}

public record AdminTranslationItem
{
    public Guid Id { get; init; }
    public string? LanguageCode { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = "";
}

public record UserStatsExportRow
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int ReviewCount { get; init; }
    public int CollectionCount { get; init; }
    public int FollowerCount { get; init; }
    public int FollowingCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
