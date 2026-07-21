using api.DbContext;

namespace api.Repository;

public class ExternalFetchStateRepository : Repository<ExternalFetchState>, IExternalFetchStateRepository
{
	public ExternalFetchStateRepository(TrackListDbContext db) : base(db) { }
}
