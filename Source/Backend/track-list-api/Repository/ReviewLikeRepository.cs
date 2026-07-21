using api.DbContext;

namespace api.Repository;

public class ReviewLikeRepository : Repository<ReviewLike>, IReviewLikeRepository
{
    public ReviewLikeRepository(TrackListDbContext db) : base(db)
    {
    }
}