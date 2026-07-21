namespace api.Models;

public class Follow : BaseEntity
{
    public Guid FollowerId { get; set; } // Хто підписався
    public virtual User? Follower { get; set; }

    public Guid FollowingId { get; set; } // На кого підписався
    public virtual User? Following { get; set; }
}