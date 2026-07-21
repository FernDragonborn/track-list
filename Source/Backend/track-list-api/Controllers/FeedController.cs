using System.Security.Claims;
using api.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
///     Feed endpoints: personal (followed users) and global.
/// </summary>
[Route("api/feed")]
[ApiController]
public class FeedController(IFeedService feedService) : ControllerBase
{
    /// <summary>
    ///     Personal feed — reviews from users the current user follows (US-301).
    /// </summary>
    [HttpGet("personal")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonalFeed(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool showShort = true)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await feedService.GetPersonalFeedAsync(userId.Value, pageNumber, pageSize, showShort);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Global feed — all reviews, newest first (US-302).
    /// </summary>
    [HttpGet("global")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalFeed(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetUserId(); // optional for guests

        var result = await feedService.GetGlobalFeedAsync(userId, pageNumber, pageSize);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     My reviews — all reviews written by the current user, newest first.
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await feedService.GetMyReviewsAsync(userId.Value, pageNumber, pageSize, sortBy);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    private Guid? GetUserId()
    {
        var idStr = User.FindFirstValue("id")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(idStr, out var guid) ? guid : null;
    }
}
