namespace api.DTOs
{
	public class TrackingStatusDto
	{
		public Guid? UserId { get; set; }
		public virtual User? User { get; set; } = null;

		public Guid MediaId { get; set; }
		public virtual Media? Media { get; set; } = null;

		public TrackingStatusCode Status { get; set; } // ENUM
		public int? Progress { get; set; } // e.g., епізод
	}
}
