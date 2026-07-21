// ReSharper disable InconsistentNaming
namespace api.DTOs;

public class TmdbEntityDto
{
    public int Id { get; set; }

    // Movie
    public string? Title { get; set; }
    public string? Release_Date { get; set; }

    // TV
    public string? Name { get; set; }
    public string? First_Air_Date { get; set; }

    // TV series
    public int? Number_Of_Seasons { get; set; }
    public int? Number_Of_Episodes { get; set; }

    // Common
    public string? Media_Type { get; set; }
    public string? Overview { get; set; }
    public string? Poster_Path { get; set; }

    // Genres: search/list endpoints return ids only, detail endpoints return object array
    public int[]? Genre_Ids { get; set; }
    public TmdbGenreObject[]? Genres { get; set; }
}

public class TmdbGenreObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public static class TmdbGenreExtractor
{
    public static int[] Extract(TmdbEntityDto dto)
    {
        if (dto.Genres is { Length: > 0 })
            return dto.Genres.Select(g => g.Id).ToArray();
        return dto.Genre_Ids ?? [];
    }
}