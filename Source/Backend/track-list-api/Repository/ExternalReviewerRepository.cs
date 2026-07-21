using api.DbContext;

namespace api.Repository;

public class ExternalReviewerRepository : Repository<ExternalReviewer>, IExternalReviewerRepository
{
	public ExternalReviewerRepository(TrackListDbContext db) : base(db) { }
}
