namespace api.Repository.IReposotory;

public interface ICommentRepository : IRepository<Comment>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload returns the
    // entity directly so callers can chain without unwrapping a Result wrapper.
    new Task<Comment> Update(Comment comment);
}