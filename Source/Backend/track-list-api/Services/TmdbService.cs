using System.Globalization;
using api.Services.IServices;
using api.Utils;
using dotenv.net;

namespace api.Services;

public sealed class TmdbService(HttpClient http, ILogger<TmdbService> logger) : IMediaExternalService
{
	private const string BaseUrl = "https://api.themoviedb.org/3";
	private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

	private static readonly IDictionary<string, string> Env = DotEnv.Read();
	private readonly bool _enabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_TMDB");
	private readonly string? _apiKey = Environment.GetEnvironmentVariable("TMDBApiKey")
	                                   ?? (Env.TryGetValue("TMDBApiKey", out var key) ? key : null);

	public async Task<Result<List<Media>>> GetByNameAsync(
		string query,
		CancellationToken cancellationToken)
	{
		if (!IsConfigured)
			return Result.Fail<List<Media>>("TMDB integration is disabled or not configured.");

		try
		{
			var url = $"{BaseUrl}/search/multi?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";

			var response = await http.GetFromJsonAsync<TmdbSearchResponse>(url, cancellationToken);

			var results = new List<Media>();
			foreach (var dto in response?.Results ?? Enumerable.Empty<TmdbEntityDto>())
			{
				if (dto.Media_Type is "person") continue;
				results.Add(MapToMedia(dto));
			}

			return Result.Ok(results);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "TMDB search failed");
			return Result.Fail<List<Media>>("TMDB search failed.");
		}
	}

	public async Task<Result<MediaTranslation>> GetTranslationAsync(string id, MediaType mediaType, string languageCode,
		CancellationToken ct)
	{
		if (!IsConfigured)
			return Result.Fail<MediaTranslation>("TMDB integration is disabled or not configured.");

		try
		{
			var endpoint = TmdbMapping.ToEndpoint(mediaType);

			var url = $"{BaseUrl}/{endpoint}/{id}?api_key={_apiKey}&language={languageCode}";

			var dto = await http.GetFromJsonAsync<TmdbEntityDto>(url, ct);
			if (dto is null) return Result.Fail<MediaTranslation>("No entity found");

			var iso639 = languageCode.Split('-', 2)[0];

			return Result.Ok(new MediaTranslation
			{
				LanguageCode = iso639,
				Title = ResolveTitle(dto, mediaType),
				Description = dto.Overview,
				Status = TranslationStatus.Pending
			});
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "TMDB translation lookup failed");
			return Result.Fail<MediaTranslation>("TMDB translation lookup failed.");
		}
	}

	// ------------------------
	// Public API
	// ------------------------

	public async Task<Result<Media>> GetByIdAsync(
		string id,
		MediaType mediaType,
		CancellationToken cancellationToken)
	{
		if (!IsConfigured)
			return Result.Fail<Media>("TMDB integration is disabled or not configured.");

		try
		{
			var endpoint = TmdbMapping.ToEndpoint(mediaType);

			var url = $"{BaseUrl}/{endpoint}/{id}?api_key={_apiKey}";
			var dto = await http.GetFromJsonAsync<TmdbEntityDto>(url, cancellationToken);

			if (dto is null) return Result.Fail<Media>("No entity found");
			return Result.Ok(MapToMedia(dto, mediaType));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "TMDB lookup failed");
			return Result.Fail<Media>("TMDB lookup failed.");
		}
	}

	// ------------------------
	// Mapping
	// ------------------------

	private bool IsConfigured => _enabled && !string.IsNullOrWhiteSpace(_apiKey);

	private static Media MapToMedia(TmdbEntityDto dto, MediaType type) =>
		new()
		{
			ExternalApiId = $"{nameof(ExternalService.Tmdb)}:{TmdbMapping.ToEndpoint(type)}:{dto.Id}",
			Type = type,
			ReleaseYear = ResolveYear(dto, type),
			PosterUrl = dto.Poster_Path != null
				? $"{ImageBaseUrl}{dto.Poster_Path}"
				: null,
			SeasonCount = type == MediaType.Series ? dto.Number_Of_Seasons : null,
			EpisodeCount = type == MediaType.Series ? dto.Number_Of_Episodes : null,
			TmdbGenreIds = TmdbGenreExtractor.Extract(dto),
			Translations =
			{
				new MediaTranslation
				{
					LanguageCode = "en",
					Title = ResolveTitle(dto, type),
					Description = dto.Overview,
					Status = TranslationStatus.Official
				}
			}
		};

	private static Media MapToMedia(TmdbEntityDto dto)
	{
		if (dto == null)
			throw new ArgumentNullException(nameof(dto));

		var type = ResolveMediaType(dto);

		var media = new Media
		{
			ExternalApiId = $"{nameof(ExternalService.Tmdb)}:{TmdbMapping.ToEndpoint(type)}:{dto.Id}",
			Type = type,
			ReleaseYear = ResolveYear(dto, type),
			PosterUrl = dto.Poster_Path != null
				? $"{ImageBaseUrl}{dto.Poster_Path}"
				: null,
			SeasonCount = type == MediaType.Series ? dto.Number_Of_Seasons : null,
			EpisodeCount = type == MediaType.Series ? dto.Number_Of_Episodes : null,
			TmdbGenreIds = TmdbGenreExtractor.Extract(dto),
		};

		if (!string.IsNullOrWhiteSpace(ResolveTitle(dto, type)) ||
		    !string.IsNullOrWhiteSpace(dto.Overview))
		{
			media.Translations.Add(new MediaTranslation
			{
				LanguageCode = "en",
				Title = ResolveTitle(dto, type),
				Description = dto.Overview,
				Status = TranslationStatus.Official
			});
		}

		return media;
	}

	private static string? ResolveTitle(TmdbEntityDto dto, MediaType type)
	{
		return type switch
		{
			MediaType.Movie => dto.Title,
			MediaType.Series => dto.Name,
			_ => null
		};
	}

	private static int? ResolveYear(TmdbEntityDto dto, MediaType type)
	{
		var raw = type switch
		{
			MediaType.Movie => dto.Release_Date,
			MediaType.Series => dto.First_Air_Date,
			_ => null
		};

		return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
			? date.Year
			: null;
	}

	private static MediaType ResolveMediaType(TmdbEntityDto dto)
	{
		if (!string.IsNullOrWhiteSpace(dto.Title) ||
		    !string.IsNullOrWhiteSpace(dto.Release_Date))
			return MediaType.Movie;

		if (!string.IsNullOrWhiteSpace(dto.Name) ||
		    !string.IsNullOrWhiteSpace(dto.First_Air_Date))
			return MediaType.Series;

		throw new InvalidOperationException(
			$"Cannot resolve media type for TMDB entity {dto.Id}");
	}
}
