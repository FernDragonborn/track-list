using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class FollowRepository : Repository<Follow>, IFollowRepository
{
    private readonly TrackListDbContext _db;

    public FollowRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public async Task<Result<Follow>> GetExistingAsync(Guid followerId, Guid followingId)
    {
        var follow = await _db.Follows
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
        return follow is null
            ? Result.Fail<Follow>("Follow not found")
            : Result.Ok(follow);
    }
}