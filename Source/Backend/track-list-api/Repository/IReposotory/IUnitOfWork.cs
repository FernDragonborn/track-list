namespace api.Repository.IReposotory;

public interface IUnitOfWork
{
    ICommentLikeRepository CommentLikeRepository { get; }
    ICommentRepository CommentRepository { get; }
    IFollowRepository FollowRepository { get; }
    IMediaRepository MediaRepository { get; }
    IMediaTranslationRepository MediaTranslationRepository { get; }
    IPlaylistRepository PlaylistRepository { get; }
    IPlaylistAccessRepository PlaylistAccessRepository { get; }
    IPlaylistItemRepository PlaylistItemRepository { get; }
    IReportRepository ReportRepository { get; }
    IReviewLikeRepository ReviewLikeRepository { get; }
    IReviewRepository ReviewRepository { get; }
    ITrackingStatusRepository TrackingStatusRepository { get; }
    IUserRepository UserRepository { get; }
    IUserImageRepository UserImageRepository { get; }
    IGenreRepository GenreRepository { get; }
    IExternalRatingRepository ExternalRatingRepository { get; }
    IExternalReviewRepository ExternalReviewRepository { get; }
    IExternalReviewerRepository ExternalReviewerRepository { get; }
    IExternalFetchStateRepository ExternalFetchStateRepository { get; }
    ITranslationRepository TranslationRepository { get; }

    Task SaveAsync();
}