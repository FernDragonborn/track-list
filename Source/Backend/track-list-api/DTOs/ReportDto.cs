namespace api.DTOs;

public record ReportTargetNavigation
{
    public string? Username { get; set; }
    public string? MediaId { get; set; }
    public string? ReviewId { get; set; }
    public string? CommentId { get; set; }

    // Content preview shown inline in moderation panel
    public string? AuthorUsername { get; set; }
    public string? ContentExcerpt { get; set; }
    public int? Rating { get; set; }          // Review only
    public string? DisplayName { get; set; }  // Profile only
    public string? Bio { get; set; }          // Profile only
    public bool IsDeleted { get; set; }       // True when the reported target has been soft-deleted
}

public record ReportDto
{
    public Guid? Id { get; set; } = null;
    public Guid TargetId { get; set; } //на кого скаржаться
    public ReportableEntityType TargetType { get; set; } // ENUM

    public ReportReason Reason { get; set; } // ENUM
    public string? Comment { get; set; } // Деталі від репортера

    public Guid ReporterId { get; set; } // Хто поскаржився
    public ReportStatus Status { get; set; } // ENUM

    // Аудит: Хто обробив скаргу
    public Guid? ProcessedByUserId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public ReportTargetNavigation? TargetNavigation { get; set; }
}

public record ResolveReportRequest
{
    /// <summary>
    ///     ResolvedDeleted = soft-delete target content; ResolvedDismissed = no action on content
    /// </summary>
    public ReportStatus Resolution { get; set; }
}
