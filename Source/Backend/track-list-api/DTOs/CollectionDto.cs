using System.ComponentModel.DataAnnotations;

namespace api.DTOs;

// ── Requests ────────────────────────────────────────────

public record CreateCollectionRequest
{
    [Required] [MaxLength(200)] public string Name { get; init; } = null!;
    [MaxLength(1000)] public string? Description { get; init; }
    public PlaylistPrivacyLevel PrivacyLevel { get; init; } = PlaylistPrivacyLevel.Public;
}

public record UpdateCollectionRequest
{
    [MaxLength(200)] public string? Name { get; init; }
    [MaxLength(1000)] public string? Description { get; init; }
    public PlaylistPrivacyLevel? PrivacyLevel { get; init; }
}

public record AddCollectionItemRequest
{
    [Required] public Guid MediaId { get; init; }
    public int? Order { get; init; }
}

public record GrantAccessRequest
{
    [Required] public Guid UserId { get; init; }
}

// ── Responses ───────────────────────────────────────────

public record CollectionResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PlaylistPrivacyLevel PrivacyLevel { get; init; }
    public Guid OwnerId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CollectionDetailResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PlaylistPrivacyLevel PrivacyLevel { get; init; }
    public Guid OwnerId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public List<CollectionItemDto> Items { get; init; } = [];
    public List<CollectionAccessDto> SharedWith { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}

public record CollectionItemDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaPosterUrl { get; init; }
    public int? Order { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CollectionMediaMembershipDto
{
    public Guid CollectionId { get; init; }
    public Guid ItemId { get; init; }
}

public record CollectionAccessDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
