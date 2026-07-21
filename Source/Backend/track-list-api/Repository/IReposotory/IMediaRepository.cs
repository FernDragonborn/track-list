namespace api.Repository.IReposotory;

public interface IMediaRepository : IRepository<Media>
{
    // Intentionally hides IRepository<T>.Update(T) — drops the Result wrapper
    // because every caller discards the return value.
    new Task Update(Media entity);
}