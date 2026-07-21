namespace api.Models;

/// <summary>
///     Елементи в "Плейлисті"
/// </summary>
public class PlaylistItem : BaseEntity
{
    public Guid CollectionId { get; set; }
    public virtual Playlist? Playlist { get; set; }

    public Guid MediaId { get; set; }
    public virtual Media? Media { get; set; }

    public int? Order { get; set; } // Для сортування
}