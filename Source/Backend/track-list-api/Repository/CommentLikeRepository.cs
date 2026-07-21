using api.DbContext;

namespace api.Repository;

public class CommentLikeRepository : Repository<CommentLike>, ICommentLikeRepository
{
    public CommentLikeRepository(TrackListDbContext db) : base(db)
    {
    }
}