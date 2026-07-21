using System.ComponentModel.DataAnnotations;

namespace api.DTOs;

public class MediaTranslationDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    [MaxLength(5)] public string? LanguageCode { get; set; } // "uk", "en"
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(10000)] public string? Description { get; set; }
}