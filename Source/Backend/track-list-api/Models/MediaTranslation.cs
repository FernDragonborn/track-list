using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Таблиця перекладів ("ключ:значення"). Вся текстова інфо тут.
/// </summary>
public class MediaTranslation : BaseEntity
{
    public Guid MediaId { get; set; }
    public virtual Media? Media { get; set; }

    [MaxLength(5)] public string? LanguageCode { get; set; } // "uk", "en" (BCP-47 2-letter)
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(10000)] public string? Description { get; set; }

    // Наш ENUM для модерації перекладів
    public TranslationStatus Status { get; set; }

    // Аудит: Хто схвалив/відхилив
    public Guid? ProcessedByUserId { get; set; }
    public virtual User? ProcessedByUser { get; set; }
}