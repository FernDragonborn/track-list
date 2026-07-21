using System.ComponentModel.DataAnnotations;

namespace api.DTOs;

public class MediaDto
{
	public Guid Id { get; set; }
	[MaxLength(500)] public string? ExternalApiId { get; set; }
	public MediaType Type { get; set; }

	public int? ReleaseYear { get; set; }
	[MaxLength(500)] public string? PosterUrl { get; set; }

	// Навігаційні властивості
	public virtual ICollection<MediaTranslationDto> Translations { get; set; } = new List<MediaTranslationDto>();
	public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}