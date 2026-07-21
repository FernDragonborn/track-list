using api.DbContext;

namespace api.Repository;

public class UserImageRepository : Repository<UserImage>, IUserImageRepository
{
    private readonly TrackListDbContext _db;

    public UserImageRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public new Task<UserImage> Update(UserImage userImage)
    {
        userImage.UpdatedAt = DateTime.UtcNow;
        _db.UserImages.Update(userImage);
        return Task.FromResult(userImage);
    }
}