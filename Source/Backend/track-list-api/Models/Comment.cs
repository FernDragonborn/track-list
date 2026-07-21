using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Коментарі до рецензій (з 1-рівневою вкладеністю)
/// </summary>
public class Comment : BaseEntity
{
    public Guid ReviewId { get; set; }
    public virtual Review? Review { get; set; }

    public Guid UserId { get; set; }
    public virtual User? User { get; set; }

    [MaxLength(10240)] public string? Content { get; set; }

    // Для вкладеності (тредів). Якщо null - це коментар Рівня 0.
    public Guid? ParentCommentId { get; set; }
    public virtual Comment? ParentComment { get; set; }

    // Навігаційні властивості
    public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public virtual ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();
}