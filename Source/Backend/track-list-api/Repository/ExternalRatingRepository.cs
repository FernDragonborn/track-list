using api.DbContext;

namespace api.Repository;

public class ExternalRatingRepository : Repository<ExternalRating>, IExternalRatingRepository
{
	public ExternalRatingRepository(TrackListDbContext db) : base(db) { }
}
