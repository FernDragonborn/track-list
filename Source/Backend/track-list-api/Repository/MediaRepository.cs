using api.DbContext;

namespace api.Repository;

public class MediaRepository : Repository<Media>, IMediaRepository
{
    private readonly TrackListDbContext _db;

    public MediaRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }
        
    public new Task Update(Media entity)
    {
        _db.Media.Update(entity);
        return Task.CompletedTask;
    }
}