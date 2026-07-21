namespace api.Utils;

public static class TmdbMapping
{
    public static string ToEndpoint(MediaType type) =>
        type switch
        {
            MediaType.Movie => "movie",
            MediaType.Series => "tv",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public static MediaType FromTmdb(string value) =>
        value switch
        {
            "movie" => MediaType.Movie,
            "tv" => MediaType.Series,
            _ => throw new ArgumentException("Unsupported TMDB media type")
        };
}