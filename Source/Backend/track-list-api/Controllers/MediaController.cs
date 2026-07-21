using api.Identity;
using api.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api.Controllers;

[Route("api/media")]
[ApiController]
public sealed class MediaController(
	IMediaGetService mediaGetService,
	IMediaOperationService mediaOps,
	IUnitOfWork unitOfWork,
	IExternalContentService externalContentService,
	ITranslationService translationService) : ControllerBase
{

	/// <summary>
	///     Browse all media with optional type/year-range/sort filters and pagination
	/// </summary>
	/// <param name="pageNumber">Page number (default 1)</param>
	/// <param name="pageSize">Items per page (default 20)</param>
	/// <param name="type">Optional filter: movie, series, book, game, other</param>
	/// <param name="yearFrom">Optional lower bound for release year (inclusive)</param>
	/// <param name="yearTo">Optional upper bound for release year (inclusive)</param>
	/// <param name="sortBy">Optional sort: added (default), year_desc, year_asc, title_asc, title_desc, rating_desc, rating_asc</param>
	/// <param name="genres">Optional comma-separated TMDB genre ids (OR semantics)</param>
	/// <param name="ct">Cancellation Token</param>
	/// <returns>Paged list of media</returns>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet("catalog")]
	public async Task<ObjectResult> GetAll(
		[FromQuery] int pageNumber = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? type = null,
		[FromQuery] int? yearFrom = null,
		[FromQuery] int? yearTo = null,
		[FromQuery] string? sortBy = null,
		[FromQuery] string? genres = null,
		CancellationToken ct = default)
	{
		MediaType? mediaType = type?.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"series" => MediaType.Series,
			"book" => MediaType.Book,
			"game" => MediaType.Game,
			"other" => MediaType.Other,
			_ => null
		};

		List<int>? genreIds = null;
		if (!string.IsNullOrWhiteSpace(genres))
		{
			genreIds = genres.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(s => int.TryParse(s, out var id) ? id : 0)
				.Where(id => id > 0)
				.ToList();
			if (genreIds.Count == 0) genreIds = null;
		}

		var result = await mediaGetService.GetAllAsync(pageNumber, pageSize, mediaType, yearFrom, yearTo, genreIds, sortBy, ct);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		var (items, totalCount) = result.Value;
		return Ok(new
		{
			data = new
			{
				items,
				totalCount,
				pageNumber,
				pageSize,
				totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
			}
		});
	}

	/// <summary>
	///     Get media details by ID (internal or external format)
	/// </summary>
	/// <param name="id">Media ID (e.g. "Tmdb:movie:123" or GUID)</param>
	/// <param name="ct">Cancellation Token</param>
	/// <returns>Media object</returns>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpGet("{id}")]
	public async Task<ObjectResult> GetMediaById(string id, CancellationToken ct)
	{
		var result = await mediaGetService.GetByIdAsync(id, ct);

		if (result.IsSuccess)
		{
			var media = result.Value;
			media.Translations = media.Translations
				.Where(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)
				.ToList();
			return Ok(new { data = media });
		}

		return NotFound(new { error = result.Error });
	}

	/// <summary>
	///     Search for media by query string
	/// </summary>
	/// <param name="query">Title to search for</param>
	/// <param name="ct">Cancellation Token</param>
	/// <returns>List of found media</returns>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[EnableRateLimiting("expensive")]
	[HttpGet("search")]
	public async Task<ObjectResult> Search([FromQuery] string query, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(query))
			return BadRequest(new { error = $"{nameof(query)} cannot be empty." });

		var result = await mediaGetService.SearchAsync(query, ct);

		if (result.IsSuccess)
		{
			foreach (var media in result.Value)
				media.Translations = media.Translations
					.Where(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)
					.ToList();
			return Ok(new { data = result.Value });
		}

		return NotFound(new { error = result.Error });
	}

	/// <summary>
	///     Create new media manually
	/// </summary>
	/// <param name="media">Media DTO</param>
	/// <returns>Created media id</returns>
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize(Policy = IdentityData.PolicyAdmin)]
	[HttpPost]
	public async Task<ObjectResult> CreateMedia([FromBody] MediaDto media)
	{
		if (!ModelState.IsValid)
			return BadRequest(new { error = "Validation failed", details = ModelState });

		// Тепер ops сервіс повертає Result<MediaDto>
		var result = await mediaOps.AddAsync(media);

		if (result.IsFailure)
		{
			return BadRequest(new { error = result.Error });
		}

		// result.Value містить DTO, яке повернув мапер (вже з ID)
		return CreatedAtAction(
			nameof(GetMediaById),
			new { id = result.Value.Id },
			new { data = result.Value });
	}

	/// <summary>
	///     Update existing media
	/// </summary>
	/// <param name="media">Media DTO to update</param>
	/// <returns>No Content</returns>
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize(Policy = IdentityData.PolicyAdmin)]
	[HttpPut]
	public async Task<IActionResult> UpdateMedia([FromBody] MediaDto media)
	{
		if (!ModelState.IsValid)
			return BadRequest(new { error = "Validation failed", details = ModelState });

		var result = await mediaOps.UpdateAsync(media);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Delete media by ID
	/// </summary>
	/// <param name="id">Media ID</param>
	/// <returns>No Content</returns>
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize(Policy = IdentityData.PolicyAdmin)]
	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteMedia(string id)
	{
		var result = await mediaOps.DeleteAsync(id);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Add translation to specific media
	/// </summary>
	/// <param name="mediaId">Target Media ID</param>
	/// <param name="translation">Translation DTO</param>
	/// <param name="ct">Cancellation token</param>
	/// <returns>Added translation</returns>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize]
	[HttpPost("{mediaId}/translations")]
	public async Task<ObjectResult> AddTranslation(
		string mediaId,
		[FromBody] MediaTranslationDto translation,
		CancellationToken ct)
	{
		// 1. Отримуємо медіа (перевірка існування)
		var mediaRes = await mediaGetService.GetByIdAsync(mediaId, ct);
		if (mediaRes.IsFailure)
			return NotFound(new { error = mediaRes.Error });

		// 2. Прив'язуємо ID
		// Примітка: mediaRes.Value має бути MediaDto. Переконайтесь, що там є поле Id (Guid)
		translation.MediaId = mediaRes.Value.Id;

		// 3. Додаємо переклад
		var opResult = await mediaOps.AddTranslationAsync(translation);

		if (opResult.IsFailure)
			return BadRequest(new { error = opResult.Error });

		return Ok(new { data = opResult.Value });
	}

	/// <summary>
	///     Suggest a new pending translation for a media item (any authenticated user).
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[Authorize]
	[HttpPost("{mediaId}/translations/suggest")]
	public async Task<IActionResult> SuggestTranslation(
		string mediaId,
		[FromBody] SuggestTranslationRequest request,
		CancellationToken ct)
	{
		var mediaRes = await mediaGetService.GetByIdAsync(mediaId, ct);
		if (mediaRes.IsFailure)
			return NotFound(new { error = mediaRes.Error });

		var dto = new MediaTranslationDto
		{
			MediaId      = mediaRes.Value.Id,
			LanguageCode = request.LanguageCode,
			Title        = request.Title,
			Description  = request.Description,
		};

		var result = await mediaOps.SuggestTranslationAsync(dto);
		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Update existing translation
	/// </summary>
	/// <param name="translationId">Translation GUID from Route</param>
	/// <param name="translation">Translation DTO from Body</param>
	/// <returns>No Content</returns>
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize(Policy = IdentityData.PolicyModerator)]
	[HttpPut("translations/{translationId:guid}")]
	public async Task<IActionResult> UpdateTranslation(
		Guid translationId,
		[FromBody] MediaTranslationDto translation)
	{
		// Валідація: чи співпадає ID в URL з ID в тілі
		if (translationId != translation.Id)
		{
			return BadRequest(new { error = "Translation ID mismatch between route and body." });
		}

		var result = await mediaOps.UpdateTranslationAsync(translation);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Delete translation
	/// </summary>
	/// <param name="translationId">Translation ID</param>
	/// <returns>No Content</returns>
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[Authorize(Policy = IdentityData.PolicyModerator)]
	[HttpDelete("translations/{translationId}")]
	public async Task<IActionResult> DeleteTranslation(string translationId)
	{
		var result = await mediaOps.DeleteTranslationAsync(translationId);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Get available genres for the given media type (movie | series).
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet("genres")]
	public async Task<ObjectResult> GetGenres([FromQuery] string type)
	{
		MediaType? target = type?.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"series" => MediaType.Series,
			_ => null
		};

		if (target is null)
			return Ok(new { data = Array.Empty<object>() });

		var t = target.Value;
		var res = await unitOfWork.GenreRepository.GetAsync(g => g.TargetType == t);
		if (res.IsFailure)
			return Ok(new { data = Array.Empty<object>() });

		var items = res.Value
			.OrderBy(g => g.NameUk)
			.Select(g => new { id = g.TmdbId, name = g.Name, nameUk = g.NameUk })
			.ToList();

		return Ok(new { data = items });
	}

	/// <summary>
	///     Get external (OMDb / Wikipedia / Letterboxd) cached content for a media.
	///     Returns immediately with stale data and queues a background refresh if TTL elapsed.
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[EnableRateLimiting("expensive")]
	[HttpGet("{mediaId:guid}/external")]
	public async Task<ObjectResult> GetExternalContent(Guid mediaId, CancellationToken ct)
	{
		var media = await mediaGetService.GetByIdAsync(mediaId.ToString(), ct);
		if (media.IsFailure)
			return NotFound(new { error = "Media not found" });

		var dto = await externalContentService.GetForMediaAsync(mediaId, ct);
		return Ok(new { data = dto });
	}

	/// <summary>
	///     Batch lookup of cached external ratings (no refresh trigger).
	///     Used by catalog cards to show IMDb / RT / Metacritic chips.
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet("external/ratings-batch")]
	public async Task<ObjectResult> GetExternalRatingsBatch([FromQuery] string ids, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(ids))
			return Ok(new { data = new Dictionary<string, List<ExternalRatingDto>>() });
		var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
			.Where(g => g != Guid.Empty)
			.ToList();
		if (parsed.Count == 0)
			return Ok(new { data = new Dictionary<string, List<ExternalRatingDto>>() });

		var result = await externalContentService.GetRatingsBatchAsync(parsed, ct);
		// Serialize keys as strings (JSON dictionaries with Guid keys are awkward)
		var strKeyed = result.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
		return Ok(new { data = strKeyed });
	}

	/// <summary>
	///     On-demand translation for the media description (TMDB synopsis).
	///     Falls back to EN translation when the user-selected language isn't natively present.
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[EnableRateLimiting("expensive")]
	[HttpGet("{mediaId:guid}/description/translate")]
	public async Task<ObjectResult> TranslateDescription(Guid mediaId, [FromQuery] string lang, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(lang))
			return BadRequest(new { error = "lang query param required" });

		var result = await translationService.TranslateMediaDescriptionAsync(mediaId, lang, ct);
		if (result.IsFailure) return NotFound(new { error = result.Error });

		return Ok(new { data = new { translation = result.Value, lang = lang.ToLowerInvariant() } });
	}

	/// <summary>
	///     On-demand translation for a single external review. Returns cached text or
	///     calls the configured translation provider and caches the result.
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[EnableRateLimiting("expensive")]
	[HttpGet("external-reviews/{reviewId:guid}/translate")]
	public async Task<ObjectResult> TranslateExternalReview(Guid reviewId, [FromQuery] string lang, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(lang))
			return BadRequest(new { error = "lang query param required" });

		var result = await translationService.TranslateExternalReviewAsync(reviewId, lang, ct);
		if (result.IsFailure) return NotFound(new { error = result.Error });

		return Ok(new { data = new { translation = result.Value, lang = lang.ToLowerInvariant() } });
	}

	/// <summary>
	///     Batch lookup combining our rating (avg + count) and external ratings for catalog cards.
	/// </summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet("ratings-batch")]
	public async Task<ObjectResult> GetRatingsBatch([FromQuery] string ids, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(ids))
			return Ok(new { data = new Dictionary<string, MediaRatingsBatchEntryDto>() });
		var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
			.Where(g => g != Guid.Empty)
			.ToList();
		if (parsed.Count == 0)
			return Ok(new { data = new Dictionary<string, MediaRatingsBatchEntryDto>() });

		var result = await externalContentService.GetMediaRatingsBatchAsync(parsed, ct);
		var strKeyed = result.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
		return Ok(new { data = strKeyed });
	}
}

public record SuggestTranslationRequest(
	string LanguageCode,
	string Title,
	string? Description);
