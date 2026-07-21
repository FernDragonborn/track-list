using api.DbContext;
using Microsoft.EntityFrameworkCore;

namespace api.Repository;

public class MediaTranslationRepository : Repository<MediaTranslation>, IMediaTranslationRepository
{
    private readonly TrackListDbContext _dbContext;

    public MediaTranslationRepository(TrackListDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public new Task Update(MediaTranslation entity)
    {
        if (_dbContext.Entry(entity).State == EntityState.Detached)
            _dbContext.MediaTranslations.Update(entity);

        return Task.CompletedTask;
    }
}