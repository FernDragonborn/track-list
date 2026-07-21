using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Epic 8: "Плейлисти" / Добірки
/// </summary>
public class Playlist : BaseEntity
{
    [MaxLength(200)] public string? Name { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }

    public Guid OwnerId { get; set; } // Власник
    public virtual User? Owner { get; set; }

    // Базовий рівень доступу (BRL-6)
    public PlaylistPrivacyLevel PrivacyLevel { get; set; } = PlaylistPrivacyLevel.Public;

    // Навігаційні властивості
    public virtual ICollection<PlaylistItem> Items { get; set; } = new List<PlaylistItem>();
    public virtual ICollection<PlaylistAccess> SharedWith { get; set; } = new List<PlaylistAccess>();
}