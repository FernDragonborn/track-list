namespace api.DTOs;

public class MediaTranslationStatusChangeDto
{
    public Guid TranslationId { get; set; }
    public TranslationStatus Status { get; set; } 
    public Guid ProcessedByUserId { get; set; }
}