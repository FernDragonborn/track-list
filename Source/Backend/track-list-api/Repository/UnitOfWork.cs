using api.DbContext;

namespace api.Repository;

public class UnitOfWork : IUnitOfWork
{
    private readonly TrackListDbContext _db;

    public IUserRepository UserRepository { get; private set; }
    public IUserImageRepository UserImageRepository { get; private set; }
    public IMediaRepository MediaRepository { get; private set; }
    public IMediaTranslationRepository MediaTranslationRepository { get; private set; }
    public ICommentLikeRepository CommentLikeRepository { get; private set; }
    public ICommentRepository CommentRepository { get; private set; }
    public IFollowRepository FollowRepository { get; private set; }
    public IPlaylistRepository PlaylistRepository { get; private set; }
    public IPlaylistAccessRepository PlaylistAccessRepository { get; private set; }
    public IPlaylistItemRepository PlaylistItemRepository { get; private set; }
    public IReportRepository ReportRepository { get; private set; }
    public IReviewLikeRepository ReviewLikeRepository { get; private set; }
    public IReviewRepository ReviewRepository { get; private set; }
    public ITrackingStatusRepository TrackingStatusRepository { get; private set; }
    public IGenreRepository GenreRepository { get; private set; }
    public IExternalRatingRepository ExternalRatingRepository { get; private set; }
    public IExternalReviewRepository ExternalReviewRepository { get; private set; }
    public IExternalReviewerRepository ExternalReviewerRepository { get; private set; }
    public IExternalFetchStateRepository ExternalFetchStateRepository { get; private set; }
    public ITranslationRepository TranslationRepository { get; private set; }

    public UnitOfWork(TrackListDbContext db)
    {
        _db = db;
        CommentLikeRepository = new CommentLikeRepository(db);
        CommentRepository = new CommentRepository(db);
        FollowRepository = new FollowRepository(db);
        MediaRepository = new MediaRepository(db);
        MediaTranslationRepository = new MediaTranslationRepository(db);
        PlaylistRepository = new PlaylistRepository(db);
        PlaylistAccessRepository = new PlaylistAccessRepository(db);
        PlaylistItemRepository = new PlaylistItemRepository(db);
        ReportRepository = new ReportRepository(db);
        ReviewLikeRepository = new ReviewLikeRepository(db);
        ReviewRepository = new ReviewRepository(db);
        TrackingStatusRepository = new TrackingStatusRepository(db);
        UserRepository = new UserRepository(db);
        UserImageRepository = new UserImageRepository(db);
        GenreRepository = new GenreRepository(db);
        ExternalRatingRepository = new ExternalRatingRepository(db);
        ExternalReviewRepository = new ExternalReviewRepository(db);
        ExternalReviewerRepository = new ExternalReviewerRepository(db);
        ExternalFetchStateRepository = new ExternalFetchStateRepository(db);
        TranslationRepository = new TranslationRepository(db);
    }

    public Task SaveAsync() => _db.SaveChangesAsync();
}
