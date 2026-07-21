using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class ReviewRepository : Repository<Review>, IReviewRepository
{
    private readonly TrackListDbContext _db;

    public ReviewRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public new Task<Review> Update(Review review)
    {
        _db.Reviews.Update(review);
        return Task.FromResult(review);
    }

    public Task<Review?> FindIncludingDeletedAsync(Guid userId, Guid mediaId) =>
        _db.Reviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == mediaId);
}