namespace api.DTOs;

public class ProfileTrackingItemDto
{
    public Guid MediaId { get; set; }
    public string? MediaTitle { get; set; }
    public string? MediaPosterUrl { get; set; }
    public string? MediaType { get; set; }
    public int? MediaEpisodeCount { get; set; }
    public TrackingStatusCode Status { get; set; }
    public int? Progress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
