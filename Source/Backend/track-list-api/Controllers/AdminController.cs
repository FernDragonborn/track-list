using System.Text;
using api.DTOs;
using api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
///     Admin-only endpoints: platform statistics and data export.
/// </summary>
[Route("api/admin")]
[ApiController]
[Authorize(Policy = IdentityData.PolicyAdmin)]
public class AdminController(IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>
    ///     Platform-wide statistics (US-703).
    ///     Optional date range; defaults to last 30 days for period-based counters.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var now  = DateTime.UtcNow;
        var from = startDate?.ToUniversalTime() ?? now.AddDays(-30);
        var to   = endDate?.ToUniversalTime()   ?? now;

        var usersRes          = await unitOfWork.UserRepository.GetAsync();
        var mediaRes          = await unitOfWork.MediaRepository.GetAsync();
        var reviewsRes        = await unitOfWork.ReviewRepository.GetAsync();
        var commentsRes       = await unitOfWork.CommentRepository.GetAsync();
        var collectionsRes    = await unitOfWork.PlaylistRepository.GetAsync();
        var reportsRes        = await unitOfWork.ReportRepository.GetAsync();
        var pendingReportsRes = await unitOfWork.ReportRepository.GetAsync(r => r.Status == ReportStatus.Pending);
        var pendingTranslRes  = await unitOfWork.MediaTranslationRepository.GetAsync(
            t => t.Status == TranslationStatus.Pending);
        var newUsersRes       = await unitOfWork.UserRepository.GetAsync(
            u => u.CreatedAt >= from && u.CreatedAt <= to);
        var newReviewsRes     = await unitOfWork.ReviewRepository.GetAsync(
            r => r.CreatedAt >= from && r.CreatedAt <= to);
        var trackingRes       = await unitOfWork.TrackingStatusRepository.GetAsync();

        var tracking = trackingRes.IsSuccess ? trackingRes.Value : [];
        var distribution = new TrackingDistributionDto(
            tracking.Count(t => t.Status == TrackingStatusCode.Planned),
            tracking.Count(t => t.Status == TrackingStatusCode.Watching),
            tracking.Count(t => t.Status == TrackingStatusCode.Completed),
            tracking.Count(t => t.Status == TrackingStatusCode.Dropped)
        );

        var stats = new PlatformStatsDto
        {
            TotalUsers           = usersRes.IsSuccess         ? usersRes.Value.Count          : 0,
            TotalMedia           = mediaRes.IsSuccess         ? mediaRes.Value.Count           : 0,
            TotalReviews         = reviewsRes.IsSuccess       ? reviewsRes.Value.Count         : 0,
            TotalComments        = commentsRes.IsSuccess      ? commentsRes.Value.Count        : 0,
            TotalCollections     = collectionsRes.IsSuccess   ? collectionsRes.Value.Count     : 0,
            TotalReports         = reportsRes.IsSuccess       ? reportsRes.Value.Count         : 0,
            PendingReports       = pendingReportsRes.IsSuccess ? pendingReportsRes.Value.Count : 0,
            PendingTranslations  = pendingTranslRes.IsSuccess  ? pendingTranslRes.Value.Count  : 0,
            NewUsersInPeriod     = newUsersRes.IsSuccess      ? newUsersRes.Value.Count        : 0,
            NewReviewsInPeriod   = newReviewsRes.IsSuccess    ? newReviewsRes.Value.Count      : 0,
            TrackingDistribution = distribution,
            StatsFrom            = from,
            StatsTo              = to,
            GeneratedAt          = now,
        };

        return Ok(new { data = stats });
    }

    /// <summary>
    ///     Paginated media list for admin management (US-702).
    /// </summary>
    [HttpGet("media")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMedia(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null)
    {
        if (pageSize > 100)
            return BadRequest(new { error = "Page size cannot exceed 100" });

        var mediaRes = await unitOfWork.MediaRepository.GetAsync(includeProperties: "Translations");
        if (mediaRes.IsFailure)
            return BadRequest(new { error = mediaRes.Error });

        var all = mediaRes.Value.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            all = all.Where(m =>
                (m.ExternalApiId ?? "").ToLower().Contains(term) ||
                m.Translations.Any(t => (t.Title ?? "").ToLower().Contains(term)));
        }

        var ordered    = all.OrderByDescending(m => m.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new AdminMediaItem
            {
                Id               = m.Id,
                ExternalApiId    = m.ExternalApiId,
                Type             = m.Type.ToString(),
                ReleaseYear      = m.ReleaseYear,
                TranslationCount = m.Translations.Count,
                Translations     = m.Translations.Select(t => new AdminTranslationItem
                {
                    Id           = t.Id,
                    LanguageCode = t.LanguageCode,
                    Title        = t.Title,
                    Description  = t.Description,
                    Status       = t.Status.ToString(),
                }).ToList(),
            })
            .ToList();

        var result = new ResponseTypes.PagedResponse<AdminMediaItem>(items, totalCount, pageNumber, pageSize);
        return Ok(new { data = result });
    }

    /// <summary>
    ///     Update any media translation (US-702). Admin can edit Official and Approved translations.
    /// </summary>
    [HttpPut("translations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTranslation(Guid id, [FromBody] UpdateTranslationRequest request)
    {
        var res = await unitOfWork.MediaTranslationRepository.GetOneAsync(t => t.Id == id);
        if (res.IsFailure)
            return NotFound(new { error = "Translation not found" });

        var translation         = res.Value;
        translation.Title       = request.Title;
        translation.Description = request.Description;
        translation.UpdatedAt   = DateTime.UtcNow;

        await unitOfWork.MediaTranslationRepository.Update(translation);
        await unitOfWork.SaveAsync();
        return Ok(new { data = "Updated" });
    }

    /// <summary>
    ///     Soft-delete a media entry (US-702). Sets DeletedAt; EF global filter hides it everywhere.
    /// </summary>
    [HttpDelete("media/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMedia(Guid id)
    {
        var res = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == id);
        if (res.IsFailure)
            return NotFound(new { error = "Media not found" });

        var media       = res.Value;
        media.DeletedAt = DateTime.UtcNow;
        media.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.MediaRepository.Update(media);
        await unitOfWork.SaveAsync();
        return NoContent();
    }

    /// <summary>
    ///     Export user data as CSV (US-703).
    /// </summary>
    [HttpGet("export/users.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportUsersCsv()
    {
        var usersRes   = await unitOfWork.UserRepository.GetAsync();
        var reviewsRes = await unitOfWork.ReviewRepository.GetAsync();
        var collectRes = await unitOfWork.PlaylistRepository.GetAsync();
        var followsRes = await unitOfWork.FollowRepository.GetAsync();

        if (usersRes.IsFailure)
            return BadRequest(new { error = usersRes.Error });

        var reviewsByUser    = (reviewsRes.IsSuccess ? reviewsRes.Value : [])
            .GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Count());
        var collectByOwner   = (collectRes.IsSuccess ? collectRes.Value : [])
            .GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Count());
        var followersByUser   = (followsRes.IsSuccess ? followsRes.Value : [])
            .GroupBy(f => f.FollowingId).ToDictionary(g => g.Key, g => g.Count());
        var followingByUser   = (followsRes.IsSuccess ? followsRes.Value : [])
            .GroupBy(f => f.FollowerId).ToDictionary(g => g.Key, g => g.Count());

        var rows = usersRes.Value.Select(u => new UserStatsExportRow
        {
            Id              = u.Id,
            Username        = u.Username,
            Email           = u.Email,
            Role            = u.Role.ToString(),
            ReviewCount     = reviewsByUser.GetValueOrDefault(u.Id, 0),
            CollectionCount = collectByOwner.GetValueOrDefault(u.Id, 0),
            FollowerCount   = followersByUser.GetValueOrDefault(u.Id, 0),
            FollowingCount  = followingByUser.GetValueOrDefault(u.Id, 0),
            CreatedAt       = u.CreatedAt,
        }).ToList();

        var csv   = BuildCsv(rows);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "users_export.csv");
    }

    private static string BuildCsv(List<UserStatsExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Username,Email,Role,ReviewCount,CollectionCount,FollowerCount,FollowingCount,CreatedAt");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.Id,
                EscapeCsv(r.Username),
                EscapeCsv(r.Email),
                r.Role,
                r.ReviewCount,
                r.CollectionCount,
                r.FollowerCount,
                r.FollowingCount,
                r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
