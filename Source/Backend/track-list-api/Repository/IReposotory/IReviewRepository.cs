namespace api.Repository.IReposotory;

public interface IReviewRepository : IRepository<Review>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload returns the
    // entity directly so callers can chain without unwrapping a Result wrapper.
    new Task<Review> Update(Review review);
    Task<Review?> FindIncludingDeletedAsync(Guid userId, Guid mediaId);
}