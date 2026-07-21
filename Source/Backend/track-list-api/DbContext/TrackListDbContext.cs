using Microsoft.EntityFrameworkCore;

namespace api.DbContext;

public class TrackListDbContext(DbContextOptions options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<CommentLike> CommentLike { get; set; } = null!;
    public DbSet<Follow> Follows { get; set; } = null!;
    public DbSet<Media> Media { get; set; } = null!;
    public DbSet<MediaTranslation> MediaTranslations { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<Playlist> Playlists { get; set; } = null!;
    public DbSet<PlaylistAccess> PlaylistAccess { get; set; } = null!;
    public DbSet<PlaylistItem> PlaylistItems { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<Review> Reviews { get; set; } = null!;
    public DbSet<ReviewLike> ReviewLikes { get; set; } = null!;
    public DbSet<TrackingStatus> TrackingStatuses { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserImage> UserImages { get; set; } = null!;
    public DbSet<ExternalRating> ExternalRatings { get; set; } = null!;
    public DbSet<ExternalReview> ExternalReviews { get; set; } = null!;
    public DbSet<ExternalReviewer> ExternalReviewers { get; set; } = null!;
    public DbSet<ExternalFetchState> ExternalFetchStates { get; set; } = null!;
    public DbSet<Translation> Translations { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning);
        }
        
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Follow relationships
        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Following)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.FollowingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: enforce unique follow relationship
        modelBuilder.Entity<Follow>()
            .HasIndex(f => new { f.FollowerId, f.FollowingId })
            .IsUnique();

        // Global Query Filters — soft delete
        modelBuilder.Entity<User>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Follow>().HasQueryFilter(f => f.DeletedAt == null);
        modelBuilder.Entity<Review>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Comment>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<ReviewLike>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<CommentLike>().HasQueryFilter(e => e.DeletedAt == null);

        // BRL-4: one review per user per media
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.MediaId })
            .IsUnique();

        // Comment self-referencing relationship
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // PlaylistItem → Playlist FK (column is "CollectionId", not EF convention "PlaylistId")
        modelBuilder.Entity<PlaylistItem>()
            .HasOne(pi => pi.Playlist)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete filters for collections
        modelBuilder.Entity<Playlist>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<PlaylistItem>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<PlaylistAccess>().HasQueryFilter(e => e.Playlist == null || e.Playlist.DeletedAt == null);

        // Unique: one media per collection
        modelBuilder.Entity<PlaylistItem>()
            .HasIndex(pi => new { pi.CollectionId, pi.MediaId })
            .IsUnique();

        // Many-to-many: Media <-> Genre
        modelBuilder.Entity<Media>()
            .HasMany(m => m.Genres)
            .WithMany(g => g.Media)
            .UsingEntity("MediaGenres");

        modelBuilder.Entity<Genre>()
            .HasIndex(g => new { g.TmdbId, g.TargetType })
            .IsUnique();

        // User has soft-delete filter; mark these required FK navs as optional
        // so EF doesn't warn about query-filtered required end.
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .IsRequired(false);

        modelBuilder.Entity<TrackingStatus>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .IsRequired(false);

        modelBuilder.Entity<UserImage>()
            .HasOne(ui => ui.User)
            .WithMany()
            .HasForeignKey(ui => ui.UserId)
            .IsRequired(false);

        // External content caches
        modelBuilder.Entity<ExternalRating>()
            .HasIndex(e => new { e.MediaId, e.Source })
            .IsUnique();
        modelBuilder.Entity<ExternalRating>()
            .HasOne(e => e.Media)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExternalReview>()
            .HasIndex(e => new { e.Source, e.ExternalRefId })
            .IsUnique();
        modelBuilder.Entity<ExternalReview>()
            .HasIndex(e => e.MediaId);
        modelBuilder.Entity<ExternalReview>()
            .HasOne(e => e.Media)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ExternalReview>()
            .HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<ExternalReview>()
            .HasOne(e => e.ExternalReviewer)
            .WithMany(r => r.Reviews)
            .HasForeignKey(e => e.ExternalReviewerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ExternalReviewer>()
            .HasIndex(e => new { e.Source, e.Handle })
            .IsUnique();
        modelBuilder.Entity<ExternalReviewer>()
            .HasQueryFilter(e => e.DeletedAt == null);

        modelBuilder.Entity<ExternalFetchState>()
            .HasIndex(e => e.MediaId)
            .IsUnique();
        modelBuilder.Entity<ExternalFetchState>()
            .HasOne(e => e.Media)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Translation>()
            .HasIndex(t => new { t.EntityType, t.EntityRefId, t.TargetLang })
            .IsUnique();
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Deleted && entry.Entity is not Follow && entry.Entity is not TrackingStatus)
            {
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}