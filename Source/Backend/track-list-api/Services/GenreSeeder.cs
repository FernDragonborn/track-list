using api.DbContext;

namespace api.Services;

/// <summary>
///     Hardcoded TMDB genre lists seeded at startup. TMDB IDs are stable.
///     Movies and TV series have different official genre lists.
/// </summary>
public static class GenreSeeder
{
    public static readonly (int Id, string En, string Uk)[] MovieGenres =
    [
        (28, "Action", "Бойовик"),
        (12, "Adventure", "Пригоди"),
        (16, "Animation", "Анімація"),
        (35, "Comedy", "Комедія"),
        (80, "Crime", "Кримінал"),
        (99, "Documentary", "Документальний"),
        (18, "Drama", "Драма"),
        (10751, "Family", "Сімейний"),
        (14, "Fantasy", "Фентезі"),
        (36, "History", "Історичний"),
        (27, "Horror", "Жахи"),
        (10402, "Music", "Музика"),
        (9648, "Mystery", "Містика"),
        (10749, "Romance", "Романтика"),
        (878, "Science Fiction", "Наукова фантастика"),
        (53, "Thriller", "Трилер"),
        (10752, "War", "Військовий"),
        (37, "Western", "Вестерн"),
    ];

    public static readonly (int Id, string En, string Uk)[] TvGenres =
    [
        (10759, "Action & Adventure", "Бойовик і пригоди"),
        (16, "Animation", "Анімація"),
        (35, "Comedy", "Комедія"),
        (80, "Crime", "Кримінал"),
        (99, "Documentary", "Документальний"),
        (18, "Drama", "Драма"),
        (10751, "Family", "Сімейний"),
        (10762, "Kids", "Дитячий"),
        (9648, "Mystery", "Містика"),
        (10763, "News", "Новини"),
        (10764, "Reality", "Реаліті"),
        (10765, "Sci-Fi & Fantasy", "Фантастика і фентезі"),
        (10766, "Soap", "Мильна опера"),
        (10767, "Talk", "Ток-шоу"),
        (10768, "War & Politics", "Війна і політика"),
        (37, "Western", "Вестерн"),
    ];

    public static void Seed(TrackListDbContext db, ILogger logger)
    {
        var existing = db.Genres.AsEnumerable()
            .ToDictionary(g => (g.TmdbId, g.TargetType), g => g);

        var inserted = 0;
        foreach (var (id, en, uk) in MovieGenres)
        {
            if (existing.ContainsKey((id, MediaType.Movie))) continue;
            db.Genres.Add(new Genre { TmdbId = id, Name = en, NameUk = uk, TargetType = MediaType.Movie });
            inserted++;
        }
        foreach (var (id, en, uk) in TvGenres)
        {
            if (existing.ContainsKey((id, MediaType.Series))) continue;
            db.Genres.Add(new Genre { TmdbId = id, Name = en, NameUk = uk, TargetType = MediaType.Series });
            inserted++;
        }

        if (inserted > 0)
        {
            db.SaveChanges();
            logger.LogInformation("GenreSeeder: inserted {Count} genres", inserted);
        }
        else
        {
            logger.LogInformation("GenreSeeder: all genres already present");
        }
    }
}
