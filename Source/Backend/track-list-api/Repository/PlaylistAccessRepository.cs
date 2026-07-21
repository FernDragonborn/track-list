using api.DbContext;

namespace api.Repository;

public class PlaylistAccessRepository : Repository<PlaylistAccess>, IPlaylistAccessRepository
{
    public PlaylistAccessRepository(TrackListDbContext db) : base(db)
    {
    }
}