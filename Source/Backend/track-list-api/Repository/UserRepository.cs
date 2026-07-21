using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class UserRepository : Repository<User>, IUserRepository
{
	private readonly TrackListDbContext _context;
	public UserRepository(TrackListDbContext context) : base(context)
	{
		_context = context;
	}
	public new Task<User> Update(User user)
	{
		user.UpdatedAt = DateTime.UtcNow;
		var updatedUser = _context.Users.Update(user);
		return Task.FromResult(updatedUser.Entity);
	}

	public async Task<Result<User>> GetDeletedOneAsync(System.Linq.Expressions.Expression<Func<User, bool>> filter)
	{
		var user = await _context.Users.IgnoreQueryFilters()
			.Where(filter)
			.Where(u => u.DeletedAt != null)
			.FirstOrDefaultAsync();
		return user is null ? Result.Fail<User>("Not found") : Result.Ok(user);
	}
}