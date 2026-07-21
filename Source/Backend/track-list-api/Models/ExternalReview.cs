using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Cached review from an external source (Letterboxd RSS, Wikipedia "Critical reception").
///     Dedupe key is (Source, ExternalRefId).
/// </summary>
public class ExternalReview : BaseEntity
{
	public Guid MediaId { get; set; }
	public virtual Media? Media { get; set; }

	/// <summary>Lowercase: letterboxd | wikipedia_reception.</summary>
	[Required] [MaxLength(30)] public string Source { get; set; } = string.Empty;

	/// <summary>Stable id within the source, e.g. "davidehrlich:dune-part-two" or "Dune_Part_Two_(2024_film)".</summary>
	[Required] [MaxLength(255)] public string ExternalRefId { get; set; } = string.Empty;

	[MaxLength(50)] public string? AuthorHandle { get; set; }
	[MaxLength(500)] public string? AuthorUrl { get; set; }

	/// <summary>
	///     FK to ExternalReviewer (virtual profile aggregation). Nullable so legacy rows survive
	///     the migration before backfill; new rows are always linked.
	/// </summary>
	public Guid? ExternalReviewerId { get; set; }
	public virtual ExternalReviewer? ExternalReviewer { get; set; }

	[Required] public string Content { get; set; } = string.Empty;

	/// <summary>1–10 if author rated it on source (Letterboxd 5-star × 2).</summary>
	public int? Rating { get; set; }

	/// <summary>Likes / hearts on source — drives our prioritization.</summary>
	public int? LikeCountOnSource { get; set; }

	[MaxLength(500)] public string? SourceUrl { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
