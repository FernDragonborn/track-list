using System.Security.Claims;
using api.DTOs;
using api.Identity;
using api.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
///     Controller for user collections (playlists / добірки).
/// </summary>
[Route("api/collections")]
[ApiController]
public class CollectionController(ICollectionService collectionService) : ControllerBase
{
    // ── CRUD ────────────────────────────────────────────

    /// <summary>
    ///     Create a new collection.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.CreateAsync(userId.Value, request);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Get collection detail by ID (respects privacy).
    /// </summary>
    [HttpGet("{collectionId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid collectionId)
    {
        var userId = GetUserId();

        var result = await collectionService.GetByIdAsync(collectionId, userId);
        if (result.IsFailure) return NotFound(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Get paginated collections for a user (owner sees all, others see public only).
    /// </summary>
    [HttpGet("user/{ownerUserId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOwner(
        Guid ownerUserId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var currentUserId = GetUserId();

        var result = await collectionService.GetByOwnerAsync(ownerUserId, currentUserId, pageNumber, pageSize);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Get current user's collection memberships for a specific media item (collectionId + itemId pairs).
    /// </summary>
    [HttpGet("memberships/{mediaId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembershipsForMedia(Guid mediaId)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.GetUserMembershipsForMediaAsync(userId.Value, mediaId);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Get public collections that contain a specific media item.
    /// </summary>
    [HttpGet("containing/{mediaId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContainingMedia(Guid mediaId)
    {
        var result = await collectionService.GetPublicContainingMediaAsync(mediaId);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Get paginated public collections.
    /// </summary>
    [HttpGet("public")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublic(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await collectionService.GetPublicAsync(pageNumber, pageSize);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Update own collection (name, description, privacy).
    /// </summary>
    [HttpPut("{collectionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid collectionId, [FromBody] UpdateCollectionRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.UpdateAsync(collectionId, userId.Value, request);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    ///     Delete a collection (owner or admin).
    /// </summary>
    [HttpDelete("{collectionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid collectionId)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var isAdmin = User.IsInRole(IdentityData.ClaimAdmin.ToString());
        var result = await collectionService.DeleteAsync(collectionId, userId.Value, isAdmin);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return NoContent();
    }

    // ── Items ───────────────────────────────────────────

    /// <summary>
    ///     Add a media item to a collection (owner or shared user).
    /// </summary>
    [HttpPost("{collectionId:guid}/items")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(Guid collectionId, [FromBody] AddCollectionItemRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.AddItemAsync(collectionId, userId.Value, request);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Remove a media item from a collection.
    /// </summary>
    [HttpDelete("{collectionId:guid}/items/{itemId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveItem(Guid collectionId, Guid itemId)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.RemoveItemAsync(collectionId, itemId, userId.Value);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    ///     Reorder a media item in a collection.
    /// </summary>
    [HttpPatch("{collectionId:guid}/items/{itemId:guid}/order")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderItem(Guid collectionId, Guid itemId, [FromQuery] int order)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.ReorderItemAsync(collectionId, itemId, userId.Value, order);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return NoContent();
    }

    // ── Access ──────────────────────────────────────────

    /// <summary>
    ///     Share collection with another user (owner only).
    /// </summary>
    [HttpPost("{collectionId:guid}/access")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GrantAccess(Guid collectionId, [FromBody] GrantAccessRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.GrantAccessAsync(collectionId, userId.Value, request);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return Ok(new { data = result.Value });
    }

    /// <summary>
    ///     Revoke access from a user (owner only).
    /// </summary>
    [HttpDelete("{collectionId:guid}/access/{targetUserId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeAccess(Guid collectionId, Guid targetUserId)
    {
        var userId = GetUserId();
        if (userId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

        var result = await collectionService.RevokeAccessAsync(collectionId, userId.Value, targetUserId);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return NoContent();
    }

    // ── Helper ──────────────────────────────────────────

    private Guid? GetUserId()
    {
        var idStr = User.FindFirstValue("id")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(idStr, out var guid) ? guid : null;
    }
}
