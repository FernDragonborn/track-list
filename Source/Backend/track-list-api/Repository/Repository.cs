using System.Diagnostics;
using System.Linq.Expressions;
using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class Repository<T> : IRepository<T> where T : class
{
	private readonly DbSet<T> _dbSet;

	protected Repository(TrackListDbContext context)
	{
		_dbSet = context.Set<T>();
	}

	public async Task<Result<T>> AddAsync(T entity)
	{
		if (entity is BaseEntity baseModel) baseModel.CreatedAt = DateTime.UtcNow;
		var entry = await _dbSet.AddAsync(entity);
		return Result.Ok(entry.Entity);
	}

	public async Task<Result<List<T>>> GetAsync(Expression<Func<T, bool>>? filter = null,
		string? includeProperties = null)
	{
		var query = _dbSet.AsQueryable();
		if (filter is not null) query = query.Where(filter);

		var propsAreIncluded = !string.IsNullOrWhiteSpace(includeProperties);
		if (propsAreIncluded)
		{
			Debug.Assert(includeProperties != null, nameof(includeProperties) + " != null");
			query = includeProperties
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Aggregate(query,
					(current, includeProperty) => current.Include(includeProperty)
				);
		}

		var fetched = await query.ToListAsync();
		return Result.Ok(fetched);
	}

	public async Task<Result<(List<T> Items, int TotalCount)>> GetPagedAsync(
		Expression<Func<T, bool>>? filter = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		int pageNumber = 1,
		int pageSize = 10,
		string? includeProperties = null)
	{
		IQueryable<T> query = _dbSet;

		if (!string.IsNullOrWhiteSpace(includeProperties))
			foreach (var prop in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
				query = query.Include(prop.Trim());

		if (filter is not null)
			query = query.Where(filter);

		// 1. Рахуємо загальну кількість ДО пагінації
		var totalCount = await query.CountAsync();

		// 2. Сортування (обов'язкове для коректної пагінації)
		if (orderBy != null)
			query = orderBy(query);
		else if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
			query = query.OrderByDescending(x => ((BaseEntity)(object)x).CreatedAt);

		// 3. Пагінація
		var items = await query
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return Result.Ok((items, totalCount));
	}


	public async Task<Result<T>> GetOneAsync(Expression<Func<T, bool>> filter, string? includeProperties = null)
	{
		var query = _dbSet.AsQueryable();
		query = query.Where(filter);

		if (!string.IsNullOrWhiteSpace(includeProperties))
		{
			query = includeProperties
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Aggregate(query,
					(current, includeProperty) => current.Include(includeProperty)
				);
		}

		var fetched = await query.FirstOrDefaultAsync();
		return fetched is null
			? Result.Fail<T>($"No such entry for filter: \'{filter.Body}\' in table {typeof(T).Name}")
			: Result.Ok(fetched);
	}

	public Task<Result> Update(T entity)
	{
		_dbSet.Update(entity);
		return Task.FromResult(Result.Ok());
	}

	public Task<Result> Remove(T entity)
	{
		_dbSet.Remove(entity);
		return Task.FromResult(Result.Ok());
	}
}
