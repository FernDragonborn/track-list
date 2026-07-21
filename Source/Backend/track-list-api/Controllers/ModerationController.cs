using System.Security.Claims;
using api.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
///     Moderation endpoints: translation queue, bulk actions.
/// </summary>
[Route("api/moderation")]
[ApiController]
[Authorize(Roles = "Admin,Moderator")]
public class ModerationController(
    IMediaOperationService mediaOps,
    IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>
    ///     Get pending translations awaiting moderation.
    /// </summary>
    [HttpGet("translations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingTranslations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await unitOfWork.MediaTranslationRepository.GetPagedAsync(
            t => t.Status == TranslationStatus.Pending,
            q => q.OrderBy(t => t.CreatedAt),
            pageNumber,
            pageSize);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var (translations, totalCount) = result.Value;

        // Fetch original (Official/Approved) translations for comparison
        var mediaIds  = translations.Select(t => t.MediaId).Distinct().ToList();
        var langCodes = translations.Select(t => t.LanguageCode).Distinct().ToList();

        var originalsRes = await unitOfWork.MediaTranslationRepository.GetAsync(
            t => (t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
                 && mediaIds.Contains(t.MediaId)
                 && langCodes.Contains(t.LanguageCode));

        var originals = (originalsRes.IsSuccess ? originalsRes.Value : [])
            .GroupBy(t => (t.MediaId, t.LanguageCode))
            .ToDictionary(g => g.Key, g => g.First());

        var dtos = translations.Select(t =>
        {
            originals.TryGetValue((t.MediaId, t.LanguageCode), out var orig);
            return new
            {
                t.Id,
                t.MediaId,
                t.LanguageCode,
                t.Title,
                t.Description,
                t.Status,
                t.CreatedAt,
                OriginalTitle       = orig?.Title,
                OriginalDescription = orig?.Description,
            };
        }).ToList();

        return Ok(new
        {
            data = new
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        });
    }

    /// <summary>
    ///     Approve or reject a pending translation.
    /// </summary>
    [HttpPost("translations/{translationId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTranslationStatus(
        Guid translationId,
        [FromBody] TranslationStatusUpdateRequest request)
    {
        if (request.Status is not (TranslationStatus.Approved or TranslationStatus.Rejected))
            return BadRequest(new { error = "Status must be Approved or Rejected." });

        var moderatorId = GetUserId();
        if (moderatorId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var dto = new MediaTranslationStatusChangeDto
        {
            TranslationId = translationId,
            Status = request.Status,
            ProcessedByUserId = moderatorId.Value
        };

        var result = await mediaOps.UpdateTranslationStatusAsync(dto);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var idStr = User.FindFirstValue("id")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(idStr, out var guid) ? guid : null;
    }
}

public record TranslationStatusUpdateRequest
{
    public TranslationStatus Status { get; init; }
}
