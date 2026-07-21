using api.DbContext;

namespace api.Repository;

public class GenreRepository : Repository<Genre>, IGenreRepository
{
    public GenreRepository(TrackListDbContext db) : base(db) { }
}
