using api.DbContext;

namespace api.Repository;

public class ExternalReviewRepository : Repository<ExternalReview>, IExternalReviewRepository
{
	public ExternalReviewRepository(TrackListDbContext db) : base(db) { }
}
