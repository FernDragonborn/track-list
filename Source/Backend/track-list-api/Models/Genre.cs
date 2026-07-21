using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Жанр медіа (TMDB lookup). Окремі списки для Movie і Series.
/// </summary>
public class Genre : BaseEntity
{
    public int TmdbId { get; set; }

    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string NameUk { get; set; } = string.Empty;

    public MediaType TargetType { get; set; }

    public virtual ICollection<Media> Media { get; set; } = new List<Media>();
}
