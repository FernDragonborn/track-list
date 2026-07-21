using api.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class MediaGetService(
	IUnitOfWork unitOfWork,
	IEnumerable<IMediaExternalService> externalServices,
	ILogger<MediaGetService> logger
) : IMediaGetService
{
	private readonly Dictionary<ExternalService, IMediaExternalService> _servicesMap =
		externalServices.ToDictionary(s => s is TmdbService
			? ExternalService.Tmdb
			: throw new NotImplementedException("Unknown service"));

	public async Task<Result<(List<Media> Items, int TotalCount)>> GetAllAsync(
		int pageNumber, int pageSize, MediaType? type, int? yearFrom, int? yearTo, List<int>? genreIds, string? sortBy, CancellationToken ct)
	{
		// Guard: swap if range inverted
		if (yearFrom.HasValue && yearTo.HasValue && yearFrom.Value > yearTo.Value)
			(yearFrom, yearTo) = (yearTo, yearFrom);

		var hasGenres = genreIds is { Count: > 0 };
		System.Linq.Expressions.Expression<Func<Media, bool>>? effectiveFilter =
			(type.HasValue || yearFrom.HasValue || yearTo.HasValue || hasGenres)
			? m => (!type.HasValue || m.Type == type.Value)
				&& (!yearFrom.HasValue || (m.ReleaseYear != null && m.ReleaseYear >= yearFrom.Value))
				&& (!yearTo.HasValue || (m.ReleaseYear != null && m.ReleaseYear <= yearTo.Value))
				&& (!hasGenres || m.Genres.Any(g => genreIds!.Contains(g.TmdbId)))
			: null;

		// Bayesian weighted rating: (v / (v + m)) * R + (m / (v + m)) * C
		// v = review count, m = minimum count threshold, R = media avg, C = global prior
		// C set BELOW typical ratings so few-review media get pulled down (e.g. 5.0 with 1 review < 4.8 with 50)
		const int m = 20;
		const double C = 3.0;

		Func<IQueryable<Media>, IOrderedQueryable<Media>> orderBy = sortBy?.ToLowerInvariant() switch
		{
			"year_asc" => q => q.OrderBy(media => media.ReleaseYear ?? int.MinValue).ThenBy(media => media.Id),
			"year_desc" => q => q.OrderByDescending(media => media.ReleaseYear ?? int.MinValue).ThenBy(media => media.Id),
			"title_asc" => q => q.OrderBy(media => media.Translations
				.Where(t => t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
				.Select(t => t.Title).FirstOrDefault() ?? "").ThenBy(media => media.Id),
			"title_desc" => q => q.OrderByDescending(media => media.Translations
				.Where(t => t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)
				.Select(t => t.Title).FirstOrDefault() ?? "").ThenBy(media => media.Id),
			// EF Core expression tree: this lambda is translated to SQL inside the
			// ORDER BY clause. Two analyzer rules are suppressed locally:
			// - S2971 (.Count property over Count()) — the .Count property of
			//   ICollection<Review> is not translatable inside an EF expression
			//   tree; only the .Count() extension method maps to SQL COUNT(*).
			// - S1155 (use .Any() instead of .Count() == 0) — the Bayesian
			//   average formula below already reuses media.Reviews.Count() three
			//   more times in the same expression, so replacing only the
			//   equality check with !Any() does not avoid the COUNT subquery and
			//   would split a single readable formula across two patterns.
#pragma warning disable S2971, S1155
			"rating_desc" => q => q.OrderByDescending(media =>
				media.Reviews.Count() == 0
					? 0.0
					: ((double)media.Reviews.Count() / (media.Reviews.Count() + m)) * media.Reviews.Average(r => (double)r.Rating)
					  + ((double)m / (media.Reviews.Count() + m)) * C).ThenBy(media => media.Id),
			"rating_asc" => q => q.OrderBy(media =>
				media.Reviews.Count() == 0
					? 0.0
					: ((double)media.Reviews.Count() / (media.Reviews.Count() + m)) * media.Reviews.Average(r => (double)r.Rating)
					  + ((double)m / (media.Reviews.Count() + m)) * C).ThenBy(media => media.Id),
#pragma warning restore S2971, S1155
			_ => q => q.OrderByDescending(media => media.CreatedAt).ThenBy(media => media.Id)
		};

		var result = await unitOfWork.MediaRepository.GetPagedAsync(
			filter: effectiveFilter,
			orderBy: orderBy,
			pageNumber: pageNumber,
			pageSize: pageSize,
			includeProperties: "Translations");

		if (result.IsSuccess)
		{
			foreach (var media in result.Value.Items)
				media.Translations = media.Translations
					.Where(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)
					.ToList();
		}

		return result;
	}

	public async Task<Result<Media>> GetByIdAsync(string fullId, CancellationToken ct)
	{
		// 0. Database GUID lookup (used when navigating from tracking/internal pages)
		if (Guid.TryParse(fullId, out var mediaGuid))
		{
			var byGuid = await unitOfWork.MediaRepository.GetOneAsync(
				m => m.Id == mediaGuid,
				"Translations,Genres");
			if (byGuid.IsSuccess)
			{
				await BackfillGenresIfMissingAsync(byGuid.Value, ct);
				return byGuid;
			}
		}

		// 1. Спочатку шукаємо в локальній базі за повним ID (наприклад "Tmdb:movie:550")
		var existingMedia = await unitOfWork.MediaRepository.GetOneAsync(
			m => m.ExternalApiId == fullId,
			"Translations,Genres");

		if (existingMedia.IsSuccess)
		{
			var cached = existingMedia.Value;
			if (cached.Type == MediaType.Series && cached.SeasonCount == null && fullId.Contains(':'))
			{
				try
				{
					var (svc, mType, extId) = ParseExternalId(fullId);
					var fresh = await GetExternalService(svc).GetByIdAsync(extId, mType, ct);
					if (fresh.IsSuccess)
					{
						cached.SeasonCount = fresh.Value.SeasonCount;
						cached.EpisodeCount = fresh.Value.EpisodeCount;
						await unitOfWork.MediaRepository.Update(cached);
						await unitOfWork.SaveAsync();
					}
				}
				catch { /* best-effort: return cached even if update fails */ }
			}
			await BackfillGenresIfMissingAsync(cached, ct);
			return Result.Ok(cached);
		}

		// 2. Якщо немає в БД - розбираємо ID і йдемо в зовнішній сервіс
		ExternalService serviceName;
		MediaType mediaType;
		string externalId;
		try
		{
			(serviceName, mediaType, externalId) = ParseExternalId(fullId);
		}
		catch (ArgumentException ex)
		{
			return Result.Fail<Media>($"Invalid ID format: {ex.Message}");
		}

		var service = GetExternalService(serviceName);

		var externalMedia = await service.GetByIdAsync(externalId, mediaType, ct);
		if (externalMedia.IsFailure) return externalMedia;

		await AttachGenresAsync(externalMedia.Value);

		var savedMediaRes = await unitOfWork.MediaRepository.AddAsync(externalMedia.Value);
		if (savedMediaRes.IsFailure)
			return Result.Fail<Media>(savedMediaRes.Error);
		await unitOfWork.SaveAsync();
		return Result.Ok(savedMediaRes.Value);
	}

	public async Task<Result<MediaTranslation>> GetTranslationAsync(string fullId, string languageCode, CancellationToken ct)
	{
		// 1. Використовуємо наш метод GetByIdAsync, щоб отримати медіа (з БД або API)
		var mediaResult = await GetByIdAsync(fullId, ct);

		if (mediaResult.IsFailure)
			return Result.Fail<MediaTranslation>(mediaResult.Error);

		var media = mediaResult.Value;

		// 2. Шукаємо переклад у вже завантаженому об'єкті
		var existingTranslation = media.Translations.FirstOrDefault(t => t.LanguageCode == languageCode);
		if (existingTranslation != null)
			return Result.Ok(existingTranslation);

		// 3. Якщо перекладу немає - йдемо в зовнішній API
		ExternalService serviceName;
		MediaType mediaType;
		string externalId;
		try
		{
			(serviceName, mediaType, externalId) = ParseExternalId(fullId);
		}
		catch (ArgumentException ex)
		{
			return Result.Fail<MediaTranslation>($"Invalid ID format: {ex.Message}");
		}

		var service = GetExternalService(serviceName);

		// Викликаємо метод сервісу, який відповідає твоїй сигнатурі
		var translationFromServiceRes = await service.GetTranslationAsync(externalId, mediaType, languageCode, ct);

		if (translationFromServiceRes.IsFailure)
			return Result.Fail<MediaTranslation>("Translation not found via API.");

		// Прив'язуємо переклад до медіа
		translationFromServiceRes.Value.MediaId = media.Id;

		// Знову ж таки, Repository.AddAsync робить SaveChanges
		var translationEntity = await unitOfWork.MediaTranslationRepository.AddAsync(translationFromServiceRes.Value);
		await unitOfWork.SaveAsync();

		return translationEntity;
	}

	public async Task<Result<List<Media>>> SearchAsync(string query, CancellationToken ct)
	{
		var resultList = new List<Media>();

		// 1. Пошук у локальній БД (тільки за Approved/Official перекладами, case-insensitive)
		// SQLite-friendly: lower both sides and use Contains. Works for Unicode via .NET String.ToLower.
		var queryLower = query.ToLower();
		var dbMediaRes = await unitOfWork.MediaRepository.GetAsync(
			m => m.Translations.Any(t =>
				t.Title != null
				&& t.Title.ToLower().Contains(queryLower)
				&& (t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved)),
			"Translations"
		);
		if (dbMediaRes.IsFailure)
			return dbMediaRes;

		resultList.AddRange(dbMediaRes.Value);

		// 2. Пошук у зовнішніх API
		var errors = new List<string>();

		foreach (var service in _servicesMap.Values)
		{
			try
			{
				// Тут GetByNameAsync повертає список.
				var externalResults = await service.GetByNameAsync(query, ct);
				if (externalResults.IsFailure) { errors.Add(externalResults.Error); continue; }

				foreach (var extMedia in externalResults.Value
					         .Where(extMedia => resultList.All(m => m.ExternalApiId != extMedia.ExternalApiId)
					         ))
				{
					var existsInDbRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.ExternalApiId == extMedia.ExternalApiId, "Translations");

					if (existsInDbRes.IsSuccess)
					{
						resultList.Add(existsInDbRes.Value);
					}
					else
					{
						var fetched = await unitOfWork.MediaRepository.AddAsync(extMedia);
						if (fetched.IsFailure) continue;
						await unitOfWork.SaveAsync();
						resultList.Add(fetched.Value);
					}
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "External media search service failed");
				errors.Add("External media search failed.");
			}
		}

		if (resultList.Count == 0)
		{
			// Якщо є помилки API та нічого не знайдено - показуємо помилки.
			// Якщо помилок немає, просто "не знайдено".
			var msg = errors.Count != 0
				? string.Join("; \n", errors)
				: "No media found.";
			return Result.Fail<List<Media>>(msg);
		}

		return Result.Ok(resultList);
	}

	// --- Helpers ---

	/// <summary>
	///     Resolve TMDB genre IDs (transient on Media.TmdbGenreIds) to persisted Genre records and attach.
	/// </summary>
	private async Task AttachGenresAsync(Media media)
	{
		var ids = media.TmdbGenreIds;
		if (ids is null || ids.Length == 0) return;

		var genresRes = await unitOfWork.GenreRepository.GetAsync(
			g => g.TargetType == media.Type && ids.Contains(g.TmdbId));

		if (genresRes.IsSuccess)
		{
			media.Genres.Clear();
			foreach (var g in genresRes.Value)
				media.Genres.Add(g);
		}
	}

	/// <summary>
	///     For existing media without genres, re-fetch from TMDB and attach.
	/// </summary>
	private async Task BackfillGenresIfMissingAsync(Media media, CancellationToken ct)
	{
		if (media.Genres.Count > 0) return;
		if (string.IsNullOrEmpty(media.ExternalApiId) || !media.ExternalApiId.Contains(':')) return;

		try
		{
			var (svc, mType, extId) = ParseExternalId(media.ExternalApiId);
			var fresh = await GetExternalService(svc).GetByIdAsync(extId, mType, ct);
			if (fresh.IsFailure || fresh.Value.TmdbGenreIds is null) return;

			media.TmdbGenreIds = fresh.Value.TmdbGenreIds;
			await AttachGenresAsync(media);

			if (media.Genres.Count > 0)
			{
				await unitOfWork.MediaRepository.Update(media);
				await unitOfWork.SaveAsync();
			}
		}
		catch { /* best-effort */ }
	}

	private IMediaExternalService GetExternalService(ExternalService serviceName)
	{
		if (_servicesMap.TryGetValue(serviceName, out var service))
		{
			return service;
		}
		throw new InvalidOperationException($"Service '{serviceName}' is not registered.");
	}

	private static (ExternalService Service, MediaType Type, string Id) ParseExternalId(string fullId)
	{
		// Очікуваний формат: "Tmdb:movie:550" або "Tmdb:series:12345"
		var parts = fullId.Split(':', 3);

		if (parts.Length != 3)
			throw new ArgumentException($"Invalid Media ID format. Expected 'Service:Type:Id', got '{fullId}'");

		if (!Enum.TryParse(parts[0], true, out ExternalService serviceName))
			throw new ArgumentException($"Unknown service '{parts[0]}'");

		// Тут нам треба розпарсити тип (movie/series) з рядка у enum MediaType.
		// Оскільки в коді TmdbService використовується TmdbMapping.ToEndpoint,
		// припустимо зворотній зв'язок або простий парсинг.
		MediaType mediaType;
		if (parts[1].Equals("movie", StringComparison.OrdinalIgnoreCase))
			mediaType = MediaType.Movie;
		else if (parts[1].Equals("tv", StringComparison.OrdinalIgnoreCase) ||
		         parts[1].Equals("series", StringComparison.OrdinalIgnoreCase))
			mediaType = MediaType.Series;
		else
			// Фоллбек, якщо формат інший, або кидаємо помилку
			throw new ArgumentException($"Unknown media type '{parts[1]}'");

		return (serviceName, mediaType, parts[2]);
	}
}
