using api.DbContext;

namespace api.Repository;

public class TrackingStatusRepository : Repository<TrackingStatus>, ITrackingStatusRepository
{
    private readonly TrackListDbContext _db;

    public TrackingStatusRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public new Task<TrackingStatus> Update(TrackingStatus trackingStatus)
    {
        _db.TrackingStatuses.Update(trackingStatus);
        return Task.FromResult(trackingStatus);
    }

    public Task<Result<List<TrackingStatus>>> GetByUserIdWithMediaAsync(Guid userId)
        => GetAsync(ts => ts.UserId == userId && ts.DeletedAt == null, "Media,Media.Translations");
}