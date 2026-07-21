using api.DbContext;

namespace api.Repository;

public class CommentRepository : Repository<Comment>, ICommentRepository
{
    private readonly TrackListDbContext _db;
    public CommentRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }
    public new Task<Comment> Update(Comment comment)
    {
        _db.Comments.Update(comment);
        return Task.FromResult(comment);
    }
}