using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Рецензія (BRL-4: 1 юзер - 1 рецензія на 1 медіа)
/// </summary>
public class Review : BaseEntity
{
    public Guid MediaId { get; set; }
    public virtual Media? Media { get; set; }

    public Guid UserId { get; set; }
    public virtual User? User { get; set; }

    public int Rating { get; set; } // 5 зірок
    [MaxLength(10000)] public string? Content { get; set; } // Rich text (HTML/Markdown)

    // Навігаційні властивості
    public virtual ICollection<ReviewLike> Likes { get; set; } = new List<ReviewLike>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}