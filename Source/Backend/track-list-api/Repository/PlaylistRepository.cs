using api.DbContext;

namespace api.Repository;

public class PlaylistRepository : Repository<Playlist>, IPlaylistRepository
{
    private readonly TrackListDbContext _db;

    public PlaylistRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public new Task<Playlist> Update(Playlist playlist)
    {
        _db.Playlists.Update(playlist);
        return Task.FromResult(playlist);
    }
}