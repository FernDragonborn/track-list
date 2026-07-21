using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class PlaylistItemRepository : Repository<PlaylistItem>, IPlaylistItemRepository
{
    private readonly TrackListDbContext _db;

    public PlaylistItemRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public async Task<Result<PlaylistItem>> FindSoftDeletedAsync(Guid collectionId, Guid mediaId)
    {
        var item = await _db.PlaylistItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.CollectionId == collectionId && i.MediaId == mediaId && i.DeletedAt != null);

        return item is null
            ? Result.Fail<PlaylistItem>("No soft-deleted item found")
            : Result.Ok(item);
    }

    public Task<Result<PlaylistItem>> RestoreAsync(PlaylistItem item)
    {
        item.DeletedAt = null;
        item.UpdatedAt = DateTime.UtcNow;
        _db.PlaylistItems.Update(item);
        return Task.FromResult(Result.Ok(item));
    }
}
