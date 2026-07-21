namespace api.Repository.IReposotory;

public interface IPlaylistItemRepository : IRepository<PlaylistItem>
{
    Task<Result<PlaylistItem>> FindSoftDeletedAsync(Guid collectionId, Guid mediaId);
    Task<Result<PlaylistItem>> RestoreAsync(PlaylistItem item);
}