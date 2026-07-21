using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Generic translation cache. Keyed by (EntityType, EntityRefId, TargetLang) — one row per
///     localized version of a piece of source content. Extensible: any new translatable surface
///     just picks a unique EntityType identifier.
/// </summary>
public class Translation : BaseEntity
{
	/// <summary>Logical kind of source content, e.g. "external_review", "media_description".</summary>
	[Required] [MaxLength(40)] public string EntityType { get; set; } = string.Empty;

	/// <summary>Stable identifier of the source within EntityType — usually a GUID or composite key.</summary>
	[Required] [MaxLength(255)] public string EntityRefId { get; set; } = string.Empty;

	/// <summary>ISO 639-1 source language code (e.g. "en"); may be auto-detected by the provider.</summary>
	[Required] [MaxLength(5)] public string SourceLang { get; set; } = string.Empty;

	/// <summary>ISO 639-1 target language code (e.g. "uk").</summary>
	[Required] [MaxLength(5)] public string TargetLang { get; set; } = string.Empty;

	[Required] public string Content { get; set; } = string.Empty;

	public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

	/// <summary>Provider that produced this translation, e.g. "deepl". For diagnostics.</summary>
	[MaxLength(20)] public string? Provider { get; set; }
}
