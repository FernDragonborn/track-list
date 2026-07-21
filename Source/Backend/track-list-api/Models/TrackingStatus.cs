namespace api.Models;

/// <summary>
///     Epic 5: Статуси перегляду (BRL-3: 1 юзер - 1 статус на 1 медіа)
/// </summary>
public class TrackingStatus : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User? User { get; set; }

    public Guid MediaId { get; set; }
    public virtual Media? Media { get; set; }

    public TrackingStatusCode Status { get; set; } // ENUM
    public int? Progress { get; set; } // e.g., епізод
}