namespace api.Models;

/// <summary>
///     Лайки для рецензій
/// </summary>
public class ReviewLike : BaseEntity // М'яке видалення = "анлайк"
{
    public Guid ReviewId { get; set; }
    public virtual Review? Review { get; set; }

    public Guid UserId { get; set; }
    public virtual User? User { get; set; }
}