namespace api.Models;

/// <summary>
///     Лайки для коментарів
/// </summary>
public class CommentLike : BaseEntity // М'яке видалення = "анлайк"
{
    public Guid CommentId { get; set; }
    public virtual Comment? Comment { get; set; }

    public Guid UserId { get; set; }
    public virtual User? User { get; set; }
}