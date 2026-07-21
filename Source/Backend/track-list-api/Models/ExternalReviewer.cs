using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Cached metadata about an external reviewer (Letterboxd critic, etc.) — denormalized
///     view that powers a "virtual profile" page without touching the real Users table.
///     Lifecycle: created lazily when first matching review is persisted (sweep or per-media
///     refresh); metadata refreshed at most once per sync interval via LastSyncedAt.
///     Dedupe key: (Source, Handle) unique.
/// </summary>
public class ExternalReviewer : BaseEntity
{
	/// <summary>Lowercase source name, currently only "letterboxd".</summary>
	[Required] [MaxLength(20)] public string Source { get; set; } = string.Empty;

	/// <summary>Stable handle within the source (e.g. "davidehrlich").</summary>
	[Required] [MaxLength(50)] public string Handle { get; set; } = string.Empty;

	/// <summary>Optional human-readable name (e.g. "David Ehrlich"). Falls back to Handle in UI.</summary>
	[MaxLength(100)] public string? DisplayName { get; set; }

	/// <summary>Short bio pulled from RSS channel description, truncated + stripped of HTML.</summary>
	[MaxLength(500)] public string? Bio { get; set; }

	[MaxLength(500)] public string? AvatarUrl { get; set; }

	/// <summary>Canonical URL of the reviewer on the source (link-back from our UI).</summary>
	[MaxLength(500)] public string? SourceProfileUrl { get; set; }

	public DateTime? LastSyncedAt { get; set; }

	public virtual ICollection<ExternalReview> Reviews { get; set; } = new List<ExternalReview>();
}
