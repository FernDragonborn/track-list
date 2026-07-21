namespace api.Repository.IReposotory;

public interface IUserImageRepository : IRepository<UserImage>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload returns the
    // entity directly so callers can chain without unwrapping a Result wrapper.
    new Task<UserImage> Update(UserImage userImage);
}