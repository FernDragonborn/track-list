namespace api.DTOs;

public class PublicStatsDto
{
	public int Users { get; set; }
	public int Media { get; set; }
	public int Movies { get; set; }
	public int Series { get; set; }
	public int Reviews { get; set; }
	public int ReviewsWithText { get; set; }
	public int Comments { get; set; }
	public double? AvgRating { get; set; }
	public DateTime ComputedAt { get; set; }
}
