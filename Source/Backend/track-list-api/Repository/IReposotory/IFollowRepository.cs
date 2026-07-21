namespace api.Repository.IReposotory;

public interface IFollowRepository : IRepository<Follow>
{
    Task<Result<Follow>> GetExistingAsync(Guid followerId, Guid followingId);
}