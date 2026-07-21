using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Cached rating from an external aggregator (OMDb → IMDb / Rotten Tomatoes / Metacritic).
///     One row per (Media, Source). Refreshed on TTL escalation 24h → 3d → 7d.
/// </summary>
public class ExternalRating : BaseEntity
{
	public Guid MediaId { get; set; }
	public virtual Media? Media { get; set; }

	/// <summary>Lowercase identifier: imdb | rotten_tomatoes | metacritic.</summary>
	[Required] [MaxLength(20)] public string Source { get; set; } = string.Empty;

	/// <summary>Normalized 0–10 score (RT 92% → 9.2; Metacritic 67 → 6.7; IMDb 8.5 → 8.5).</summary>
	public double Score { get; set; }

	/// <summary>What the source reported verbatim, e.g. "8.5/10", "92%", "67/100".</summary>
	[MaxLength(20)] public string? RawScore { get; set; }

	/// <summary>Reported by IMDb only; null for RT and Metacritic.</summary>
	public int? VoteCount { get; set; }

	public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
