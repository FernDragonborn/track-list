namespace api.DTOs;

public class TmdbTranslationDto
{
	public string Iso_639_1 { get; set; } = null!;
	public string? Title { get; set; }
	public string? Overview { get; set; }
}