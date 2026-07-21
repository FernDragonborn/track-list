namespace api.Repository.IReposotory;

public interface IUserRepository:IRepository<User>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload sets
    // user.UpdatedAt and returns the entity directly so AuthService and
    // UserService can chain without unwrapping a Result wrapper.
    new Task<User> Update(User user);
    Task<Result<User>> GetDeletedOneAsync(System.Linq.Expressions.Expression<Func<User, bool>> filter);
}