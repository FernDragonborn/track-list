using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Per-media bookkeeping that drives the TTL refresh schedule:
///     1st fetch → +24h, 2nd → +3d, 3rd+ → +7d.
/// </summary>
public class ExternalFetchState : BaseEntity
{
	public Guid MediaId { get; set; }
	public virtual Media? Media { get; set; }

	public int FetchCount { get; set; }
	public DateTime? LastFetchedAt { get; set; }
	public DateTime? NextFetchDueAt { get; set; }
	public DateTime? LastErrorAt { get; set; }
	[MaxLength(500)] public string? LastError { get; set; }
}
