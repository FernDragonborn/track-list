namespace api.Repository.IReposotory;

public interface ITrackingStatusRepository : IRepository<TrackingStatus>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload returns the
    // entity directly so TrackingStatusController can wrap it in Result.Ok.
    new Task<TrackingStatus> Update(TrackingStatus trackingStatus);
    Task<Result<List<TrackingStatus>>> GetByUserIdWithMediaAsync(Guid userId);
}