namespace api.Models;

/// <summary>
///     Epic 8: Таблиця доступу в стилі "Google Docs".
///     НЕ успадковує BaseEntity (видалення тут "жорстке").
/// </summary>
public class PlaylistAccess
{
    public Guid Id { get; set; } // Guid v7

    public Guid PlaylistId { get; set; }
    public virtual Playlist? Playlist { get; set; }

    public Guid UserId { get; set; } // Користувач, якому надали доступ
    public virtual User? User { get; set; }

    public DateTime CreatedAt { get; set; }
}