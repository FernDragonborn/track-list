namespace api.Repository.IReposotory;

public interface IMediaTranslationRepository : IRepository<MediaTranslation>
{
    // Intentionally hides IRepository<T>.Update(T) — drops the Result wrapper
    // because every caller discards the return value.
    new Task Update(MediaTranslation entity);
}