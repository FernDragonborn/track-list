using api.DTOs;
using api.Services.IServices;
using api.Utils;
using static api.DTOs.ResponseTypes;

namespace api.Services;

public class CollectionService(IUnitOfWork unitOfWork) : ICollectionService
{
    // ── CRUD ────────────────────────────────────────────────────

    public async Task<Result<CollectionResponseDto>> CreateAsync(Guid userId, CreateCollectionRequest req)
    {
        if (req.Name.Equals(CollectionConstants.DefaultCollectionName, StringComparison.OrdinalIgnoreCase))
            return Result.Fail<CollectionResponseDto>($"Назва «{CollectionConstants.DefaultCollectionName}» зарезервована");

        var playlist = new Playlist
        {
            OwnerId = userId,
            Name = req.Name,
            Description = req.Description,
            PrivacyLevel = req.PrivacyLevel
        };

        var addRes = await unitOfWork.PlaylistRepository.AddAsync(playlist);
        if (addRes.IsFailure)
            return Result.Fail<CollectionResponseDto>(addRes.Error);

        await unitOfWork.SaveAsync();
        var username = await GetUsername(userId);
        return Result.Ok(MapToResponse(addRes.Value, username, 0));
    }

    public async Task<Result<CollectionDetailResponseDto>> GetByIdAsync(Guid collectionId, Guid? currentUserId)
    {
        var res = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (res.IsFailure)
            return Result.Fail<CollectionDetailResponseDto>("Collection not found");

        var playlist = res.Value;

        if (!await CanViewAsync(playlist, currentUserId))
            return Result.Fail<CollectionDetailResponseDto>("Collection not found");

        var username = await GetUsername(playlist.OwnerId);

        // Items
        var itemsRes = await unitOfWork.PlaylistItemRepository.GetAsync(
            i => i.CollectionId == collectionId);
        var items = itemsRes.IsSuccess ? itemsRes.Value : [];

        var itemDtos = new List<CollectionItemDto>();
        foreach (var item in items.OrderBy(i => i.Order ?? int.MaxValue).ThenBy(i => i.CreatedAt))
        {
            var mediaTitle = await GetMediaTitle(item.MediaId);
            var mediaPosterUrl = await GetMediaPosterUrl(item.MediaId);
            itemDtos.Add(new CollectionItemDto
            {
                Id = item.Id,
                MediaId = item.MediaId,
                MediaTitle = mediaTitle,
                MediaPosterUrl = mediaPosterUrl,
                Order = item.Order,
                CreatedAt = item.CreatedAt
            });
        }

        // Shared users (only visible to owner)
        var accessDtos = new List<CollectionAccessDto>();
        if (currentUserId == playlist.OwnerId)
        {
            var accessRes = await unitOfWork.PlaylistAccessRepository.GetAsync(
                a => a.PlaylistId == collectionId);
            if (accessRes.IsSuccess)
            {
                foreach (var access in accessRes.Value)
                {
                    var accessUsername = await GetUsername(access.UserId);
                    accessDtos.Add(new CollectionAccessDto
                    {
                        Id = access.Id,
                        UserId = access.UserId,
                        Username = accessUsername,
                        CreatedAt = access.CreatedAt
                    });
                }
            }
        }

        return Result.Ok(new CollectionDetailResponseDto
        {
            Id = playlist.Id,
            Name = playlist.Name ?? string.Empty,
            Description = playlist.Description,
            PrivacyLevel = playlist.PrivacyLevel,
            OwnerId = playlist.OwnerId,
            OwnerUsername = username,
            Items = itemDtos,
            SharedWith = accessDtos,
            CreatedAt = playlist.CreatedAt
        });
    }

    public async Task<Result<PagedResponse<CollectionResponseDto>>> GetByOwnerAsync(
        Guid ownerUserId, Guid? currentUserId, int pageNumber, int pageSize)
    {
        var isOwner = currentUserId == ownerUserId;

        var pagedRes = await unitOfWork.PlaylistRepository.GetPagedAsync(
            p => p.OwnerId == ownerUserId
                 && (isOwner
                     || p.PrivacyLevel == PlaylistPrivacyLevel.Public
                     || (currentUserId.HasValue && p.SharedWith.Any(a => a.UserId == currentUserId.Value))),
            q => q.OrderByDescending(p => p.CreatedAt),
            pageNumber,
            pageSize);

        if (pagedRes.IsFailure)
            return Result.Fail<PagedResponse<CollectionResponseDto>>(pagedRes.Error);

        var (playlists, totalCount) = pagedRes.Value;
        var username = await GetUsername(ownerUserId);

        var dtos = new List<CollectionResponseDto>();
        foreach (var p in playlists)
        {
            var itemCount = await GetItemCount(p.Id);
            dtos.Add(MapToResponse(p, username, itemCount));
        }

        return Result.Ok(new PagedResponse<CollectionResponseDto>(dtos, totalCount, pageNumber, pageSize));
    }

    public async Task<Result<PagedResponse<CollectionResponseDto>>> GetPublicAsync(int pageNumber, int pageSize)
    {
        var pagedRes = await unitOfWork.PlaylistRepository.GetPagedAsync(
            p => p.PrivacyLevel == PlaylistPrivacyLevel.Public,
            q => q.OrderByDescending(p => p.CreatedAt),
            pageNumber,
            pageSize);

        if (pagedRes.IsFailure)
            return Result.Fail<PagedResponse<CollectionResponseDto>>(pagedRes.Error);

        var (playlists, totalCount) = pagedRes.Value;
        var dtos = new List<CollectionResponseDto>();

        foreach (var p in playlists)
        {
            var username = await GetUsername(p.OwnerId);
            var itemCount = await GetItemCount(p.Id);
            dtos.Add(MapToResponse(p, username, itemCount));
        }

        return Result.Ok(new PagedResponse<CollectionResponseDto>(dtos, totalCount, pageNumber, pageSize));
    }

    public async Task<Result<List<CollectionResponseDto>>> GetPublicContainingMediaAsync(Guid mediaId)
    {
        var itemsRes = await unitOfWork.PlaylistItemRepository.GetAsync(i => i.MediaId == mediaId);
        if (itemsRes.IsFailure)
            return Result.Ok(new List<CollectionResponseDto>());

        var collectionIds = itemsRes.Value.Select(i => i.CollectionId).Distinct();
        var result = new List<CollectionResponseDto>();

        foreach (var collectionId in collectionIds)
        {
            var playlistRes = await unitOfWork.PlaylistRepository.GetOneAsync(
                p => p.Id == collectionId && p.PrivacyLevel == PlaylistPrivacyLevel.Public);
            if (playlistRes.IsFailure) continue;

            var username = await GetUsername(playlistRes.Value.OwnerId);
            var itemCount = await GetItemCount(collectionId);
            result.Add(MapToResponse(playlistRes.Value, username, itemCount));
        }

        return Result.Ok(result);
    }

    public async Task<Result<List<CollectionMediaMembershipDto>>> GetUserMembershipsForMediaAsync(Guid userId, Guid mediaId)
    {
        var playlistsRes = await unitOfWork.PlaylistRepository.GetAsync(p => p.OwnerId == userId);
        if (playlistsRes.IsFailure)
            return Result.Ok(new List<CollectionMediaMembershipDto>());

        var result = new List<CollectionMediaMembershipDto>();
        foreach (var playlist in playlistsRes.Value)
        {
            var itemRes = await unitOfWork.PlaylistItemRepository.GetOneAsync(
                i => i.CollectionId == playlist.Id && i.MediaId == mediaId);
            if (itemRes.IsSuccess)
                result.Add(new CollectionMediaMembershipDto { CollectionId = playlist.Id, ItemId = itemRes.Value.Id });
        }

        return Result.Ok(result);
    }

    public async Task<Result> UpdateAsync(Guid collectionId, Guid userId, UpdateCollectionRequest req)
    {
        var res = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (res.IsFailure)
            return Result.Fail("Collection not found");

        var playlist = res.Value;
        if (playlist.OwnerId != userId)
            return Result.Fail("Only the owner can update this collection");

        var isDefault = playlist.Name?.Equals(CollectionConstants.DefaultCollectionName, StringComparison.OrdinalIgnoreCase) == true;

        if (req.Name is not null)
        {
            if (isDefault && !req.Name.Equals(CollectionConstants.DefaultCollectionName, StringComparison.OrdinalIgnoreCase))
                return Result.Fail("Назву цієї добірки не можна змінити");
            if (!isDefault && req.Name.Equals(CollectionConstants.DefaultCollectionName, StringComparison.OrdinalIgnoreCase))
                return Result.Fail($"Назва «{CollectionConstants.DefaultCollectionName}» зарезервована");
            playlist.Name = req.Name;
        }
        if (req.Description is not null) playlist.Description = req.Description;
        if (req.PrivacyLevel is not null) playlist.PrivacyLevel = req.PrivacyLevel.Value;

        await unitOfWork.PlaylistRepository.Update(playlist);
        await unitOfWork.SaveAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid collectionId, Guid userId, bool isAdmin)
    {
        var res = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (res.IsFailure)
            return Result.Fail("Collection not found");

        var playlist = res.Value;
        if (playlist.Name?.Equals(CollectionConstants.DefaultCollectionName, StringComparison.OrdinalIgnoreCase) == true)
            return Result.Fail($"Добірку «{CollectionConstants.DefaultCollectionName}» не можна видалити");

        if (playlist.OwnerId != userId && !isAdmin)
            return Result.Fail("Only the owner or an admin can delete this collection");

        var removeRes = await unitOfWork.PlaylistRepository.Remove(playlist);
        if (removeRes.IsFailure)
            return Result.Fail(removeRes.Error);

        await unitOfWork.SaveAsync();
        return Result.Ok();
    }

    // ── Items ───────────────────────────────────────────────────

    public async Task<Result<CollectionItemDto>> AddItemAsync(
        Guid collectionId, Guid userId, AddCollectionItemRequest req)
    {
        var access = await VerifyWriteAccess(collectionId, userId);
        if (access.IsFailure)
            return Result.Fail<CollectionItemDto>(access.Error);

        // Check media exists
        var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == req.MediaId);
        if (mediaRes.IsFailure)
            return Result.Fail<CollectionItemDto>("Media not found");

        // Check duplicate (active)
        var existingRes = await unitOfWork.PlaylistItemRepository.GetOneAsync(
            i => i.CollectionId == collectionId && i.MediaId == req.MediaId);
        if (existingRes.IsSuccess)
            return Result.Fail<CollectionItemDto>("Media already in this collection");

        // Restore soft-deleted item if present (avoids unique-constraint violation on re-add)
        var deletedRes = await unitOfWork.PlaylistItemRepository.FindSoftDeletedAsync(collectionId, req.MediaId);
        if (deletedRes.IsSuccess)
        {
            var restoreRes = await unitOfWork.PlaylistItemRepository.RestoreAsync(deletedRes.Value);
            if (restoreRes.IsFailure)
                return Result.Fail<CollectionItemDto>(restoreRes.Error);

            await unitOfWork.SaveAsync();
            var mediaTitle2 = await GetMediaTitle(req.MediaId);
            var mediaPosterUrl2 = await GetMediaPosterUrl(req.MediaId);
            return Result.Ok(new CollectionItemDto
            {
                Id = restoreRes.Value.Id,
                MediaId = restoreRes.Value.MediaId,
                MediaTitle = mediaTitle2,
                MediaPosterUrl = mediaPosterUrl2,
                Order = restoreRes.Value.Order,
                CreatedAt = restoreRes.Value.CreatedAt
            });
        }

        var item = new PlaylistItem
        {
            CollectionId = collectionId,
            MediaId = req.MediaId,
            Order = req.Order
        };

        var addRes = await unitOfWork.PlaylistItemRepository.AddAsync(item);
        if (addRes.IsFailure)
            return Result.Fail<CollectionItemDto>(addRes.Error);

        await unitOfWork.SaveAsync();
        var mediaTitle = await GetMediaTitle(req.MediaId);
        var mediaPosterUrl = await GetMediaPosterUrl(req.MediaId);

        return Result.Ok(new CollectionItemDto
        {
            Id = addRes.Value.Id,
            MediaId = addRes.Value.MediaId,
            MediaTitle = mediaTitle,
            MediaPosterUrl = mediaPosterUrl,
            Order = addRes.Value.Order,
            CreatedAt = addRes.Value.CreatedAt
        });
    }

    public async Task<Result> RemoveItemAsync(Guid collectionId, Guid itemId, Guid userId)
    {
        var access = await VerifyWriteAccess(collectionId, userId);
        if (access.IsFailure)
            return Result.Fail(access.Error);

        var itemRes = await unitOfWork.PlaylistItemRepository.GetOneAsync(
            i => i.Id == itemId && i.CollectionId == collectionId);
        if (itemRes.IsFailure)
            return Result.Fail("Item not found");

        var removeRes = await unitOfWork.PlaylistItemRepository.Remove(itemRes.Value);
        if (removeRes.IsFailure)
            return Result.Fail(removeRes.Error);

        await unitOfWork.SaveAsync();
        return Result.Ok();
    }

    public async Task<Result> ReorderItemAsync(Guid collectionId, Guid itemId, Guid userId, int newOrder)
    {
        var access = await VerifyWriteAccess(collectionId, userId);
        if (access.IsFailure)
            return Result.Fail(access.Error);

        var itemRes = await unitOfWork.PlaylistItemRepository.GetOneAsync(
            i => i.Id == itemId && i.CollectionId == collectionId);
        if (itemRes.IsFailure)
            return Result.Fail("Item not found");

        var item = itemRes.Value;
        item.Order = newOrder;

        // PlaylistItem inherits BaseEntity → use repository's generic update via context
        // The item is tracked, so SaveChanges on next repo call picks it up
        // But we need an explicit save — use the playlist repo's Update indirectly
        // Simplest: re-add after remove would lose the ID. Instead, direct context save via repo.
        // PlaylistItemRepository has no Update — use the tracked entity + playlist repo update trick
        // Actually the entity is tracked by EF. We need a save. Let's use the PlaylistRepository's Update
        // to trigger SaveChangesAsync on the same context.
        await unitOfWork.SaveAsync();

        return Result.Ok();
    }

    // ── Access ──────────────────────────────────────────────────

    public async Task<Result<CollectionAccessDto>> GrantAccessAsync(
        Guid collectionId, Guid ownerId, GrantAccessRequest req)
    {
        var playlistRes = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (playlistRes.IsFailure)
            return Result.Fail<CollectionAccessDto>("Collection not found");

        if (playlistRes.Value.OwnerId != ownerId)
            return Result.Fail<CollectionAccessDto>("Only the owner can share this collection");

        if (req.UserId == ownerId)
            return Result.Fail<CollectionAccessDto>("Cannot share with yourself");

        // Check user exists
        var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == req.UserId);
        if (userRes.IsFailure)
            return Result.Fail<CollectionAccessDto>("User not found");

        // Check not already shared
        var existingRes = await unitOfWork.PlaylistAccessRepository.GetOneAsync(
            a => a.PlaylistId == collectionId && a.UserId == req.UserId);
        if (existingRes.IsSuccess)
            return Result.Fail<CollectionAccessDto>("User already has access");

        var access = new PlaylistAccess
        {
            Id = Guid.CreateVersion7(),
            PlaylistId = collectionId,
            UserId = req.UserId,
            CreatedAt = DateTime.UtcNow
        };

        var addRes = await unitOfWork.PlaylistAccessRepository.AddAsync(access);
        if (addRes.IsFailure)
            return Result.Fail<CollectionAccessDto>(addRes.Error);

        await unitOfWork.SaveAsync();
        return Result.Ok(new CollectionAccessDto
        {
            Id = addRes.Value.Id,
            UserId = addRes.Value.UserId,
            Username = userRes.Value.Username ?? string.Empty,
            CreatedAt = addRes.Value.CreatedAt
        });
    }

    public async Task<Result> RevokeAccessAsync(Guid collectionId, Guid ownerId, Guid targetUserId)
    {
        var playlistRes = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (playlistRes.IsFailure)
            return Result.Fail("Collection not found");

        if (playlistRes.Value.OwnerId != ownerId)
            return Result.Fail("Only the owner can revoke access");

        var accessRes = await unitOfWork.PlaylistAccessRepository.GetOneAsync(
            a => a.PlaylistId == collectionId && a.UserId == targetUserId);
        if (accessRes.IsFailure)
            return Result.Fail("Access entry not found");

        var removeRes = await unitOfWork.PlaylistAccessRepository.Remove(accessRes.Value);
        if (removeRes.IsFailure)
            return Result.Fail(removeRes.Error);

        await unitOfWork.SaveAsync();
        return Result.Ok();
    }

    // ── Helpers ─────────────────────────────────────────────────

    private async Task<bool> CanViewAsync(Playlist playlist, Guid? currentUserId)
    {
        if (playlist.PrivacyLevel == PlaylistPrivacyLevel.Public)
            return true;

        if (currentUserId is null)
            return false;

        if (playlist.OwnerId == currentUserId.Value)
            return true;

        var accessRes = await unitOfWork.PlaylistAccessRepository
            .GetOneAsync(a => a.PlaylistId == playlist.Id && a.UserId == currentUserId.Value);

        return accessRes.IsSuccess;
    }

    private async Task<Result> VerifyWriteAccess(Guid collectionId, Guid userId)
    {
        var res = await unitOfWork.PlaylistRepository.GetOneAsync(p => p.Id == collectionId);
        if (res.IsFailure)
            return Result.Fail("Collection not found");

        var playlist = res.Value;

        // Owner always has write access
        if (playlist.OwnerId == userId)
            return Result.Ok();

        // Shared users have write access
        var accessRes = await unitOfWork.PlaylistAccessRepository.GetOneAsync(
            a => a.PlaylistId == collectionId && a.UserId == userId);
        if (accessRes.IsSuccess)
            return Result.Ok();

        return Result.Fail("You do not have access to this collection");
    }

    private async Task<string> GetUsername(Guid userId)
    {
        var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userId);
        return userRes.IsSuccess ? (userRes.Value.Username ?? string.Empty) : string.Empty;
    }

    private async Task<string?> GetMediaTitle(Guid mediaId)
    {
        var translationRes = await unitOfWork.MediaTranslationRepository.GetOneAsync(
            t => t.MediaId == mediaId);
        return translationRes.IsSuccess ? translationRes.Value.Title : null;
    }

    private async Task<string?> GetMediaPosterUrl(Guid mediaId)
    {
        var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == mediaId);
        return mediaRes.IsSuccess ? mediaRes.Value.PosterUrl : null;
    }

    private async Task<int> GetItemCount(Guid playlistId)
    {
        var itemsRes = await unitOfWork.PlaylistItemRepository.GetAsync(
            i => i.CollectionId == playlistId);
        return itemsRes.IsSuccess ? itemsRes.Value.Count : 0;
    }

    private static CollectionResponseDto MapToResponse(Playlist p, string ownerUsername, int itemCount) =>
        new()
        {
            Id = p.Id,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            PrivacyLevel = p.PrivacyLevel,
            OwnerId = p.OwnerId,
            OwnerUsername = ownerUsername,
            ItemCount = itemCount,
            CreatedAt = p.CreatedAt
        };
}
