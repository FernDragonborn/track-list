using api.DbContext;

namespace api.Repository;

public class TranslationRepository : Repository<Translation>, ITranslationRepository
{
	public TranslationRepository(TrackListDbContext db) : base(db) { }
}
