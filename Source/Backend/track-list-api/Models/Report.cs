using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
///     Epic 6: Таблиця скарг (репортів)
/// </summary>
public class Report : BaseEntity
{
	// ID того, на що скаржаться (Review, Comment, User)
	public Guid TargetId { get; set; }
	public ReportableEntityType TargetType { get; set; } // ENUM

	public ReportReason Reason { get; set; } // ENUM

	[MaxLength(10000)]
	public string? Comment { get; set; } // Деталі від репортера

	public Guid ReporterId { get; set; } // Хто поскаржився
	public virtual User? Reporter { get; set; }

	public ReportStatus Status { get; set; } // ENUM

	// Аудит: Хто обробив скаргу
	public Guid? ProcessedByUserId { get; set; }
	public virtual User? ProcessedByUser { get; set; }
}