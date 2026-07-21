using api.DTOs;
using static api.DTOs.ResponseTypes;

namespace api.Services.IServices;

public interface ICollectionService
{
    // ── CRUD ────────────────────────────────────────────
    Task<Result<CollectionResponseDto>> CreateAsync(Guid userId, CreateCollectionRequest req);
    Task<Result<CollectionDetailResponseDto>> GetByIdAsync(Guid collectionId, Guid? currentUserId);
    Task<Result<PagedResponse<CollectionResponseDto>>> GetByOwnerAsync(Guid ownerUserId, Guid? currentUserId, int pageNumber, int pageSize);
    Task<Result<PagedResponse<CollectionResponseDto>>> GetPublicAsync(int pageNumber, int pageSize);
    Task<Result<List<CollectionResponseDto>>> GetPublicContainingMediaAsync(Guid mediaId);
    Task<Result<List<CollectionMediaMembershipDto>>> GetUserMembershipsForMediaAsync(Guid userId, Guid mediaId);
    Task<Result> UpdateAsync(Guid collectionId, Guid userId, UpdateCollectionRequest req);
    Task<Result> DeleteAsync(Guid collectionId, Guid userId, bool isAdmin);

    // ── Items ───────────────────────────────────────────
    Task<Result<CollectionItemDto>> AddItemAsync(Guid collectionId, Guid userId, AddCollectionItemRequest req);
    Task<Result> RemoveItemAsync(Guid collectionId, Guid itemId, Guid userId);
    Task<Result> ReorderItemAsync(Guid collectionId, Guid itemId, Guid userId, int newOrder);

    // ── Access (sharing) ────────────────────────────────
    Task<Result<CollectionAccessDto>> GrantAccessAsync(Guid collectionId, Guid ownerId, GrantAccessRequest req);
    Task<Result> RevokeAccessAsync(Guid collectionId, Guid ownerId, Guid targetUserId);
}
