using api.Services.External;
using api.Services.IServices;

namespace api.Services;

public class TranslationService : ITranslationService
{
	private readonly IUnitOfWork _uow;
	private readonly DeeplClient _deepl;
	private readonly ILogger<TranslationService> _log;

	public TranslationService(IUnitOfWork uow, DeeplClient deepl, ILogger<TranslationService> log)
	{
		_uow = uow;
		_deepl = deepl;
		_log = log;
	}

	public async Task<string?> GetCachedAsync(string entityType, string entityRefId, string targetLang, CancellationToken ct)
	{
		var key = targetLang.ToLowerInvariant();
		var res = await _uow.TranslationRepository.GetOneAsync(t =>
			t.EntityType == entityType && t.EntityRefId == entityRefId && t.TargetLang == key);
		return res.IsSuccess ? res.Value.Content : null;
	}

	public async Task<string?> TranslateAndCacheAsync(
		string entityType,
		string entityRefId,
		string sourceText,
		string targetLang,
		CancellationToken ct,
		string sourceLangHint = "en")
	{
		if (string.IsNullOrWhiteSpace(sourceText)) return null;
		var target = targetLang.ToLowerInvariant();

		var cached = await GetCachedAsync(entityType, entityRefId, target, ct);
		if (cached is not null) return cached;

		var result = await _deepl.TranslateAsync(sourceText, target, ct);
		if (result is null) return null;
		var detected = string.IsNullOrEmpty(result.SourceLang) ? sourceLangHint : result.SourceLang.ToLowerInvariant();

		// If DeepL detected target == source it returns the original; don't cache a no-op.
		if (string.Equals(detected, target, StringComparison.OrdinalIgnoreCase))
			return result.Text;

		// Upsert (race-safe single attempt — unique index will catch dupes)
		try
		{
			await _uow.TranslationRepository.AddAsync(new Translation
			{
				Id = Guid.NewGuid(),
				EntityType = entityType,
				EntityRefId = entityRefId,
				SourceLang = detected,
				TargetLang = target,
				Content = result.Text,
				FetchedAt = DateTime.UtcNow,
				Provider = "deepl",
			});
			await _uow.SaveAsync();
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to cache translation {Type}/{Id} -> {Lang}", entityType, entityRefId, target);
		}
		return result.Text;
	}

	public async Task<Result<string>> TranslateReviewAsync(Guid reviewId, string targetLang, CancellationToken ct)
	{
		var res = await _uow.ReviewRepository.GetOneAsync(r => r.Id == reviewId);
		if (res.IsFailure || string.IsNullOrWhiteSpace(res.Value.Content))
			return Result.Fail<string>("Review not found or has no content");
		var t = await TranslateAndCacheAsync("review", reviewId.ToString(), res.Value.Content!, targetLang, ct);
		return t is null ? Result.Fail<string>("Translation failed") : Result.Ok(t);
	}

	public async Task<Result<string>> TranslateCommentAsync(Guid commentId, string targetLang, CancellationToken ct)
	{
		var res = await _uow.CommentRepository.GetOneAsync(c => c.Id == commentId);
		if (res.IsFailure || string.IsNullOrWhiteSpace(res.Value.Content))
			return Result.Fail<string>("Comment not found or has no content");
		var t = await TranslateAndCacheAsync("comment", commentId.ToString(), res.Value.Content!, targetLang, ct);
		return t is null ? Result.Fail<string>("Translation failed") : Result.Ok(t);
	}

	public async Task<Result<string>> TranslateExternalReviewAsync(Guid externalReviewId, string targetLang, CancellationToken ct)
	{
		var res = await _uow.ExternalReviewRepository.GetOneAsync(r => r.Id == externalReviewId);
		if (res.IsFailure || string.IsNullOrWhiteSpace(res.Value.Content))
			return Result.Fail<string>("External review not found");
		var t = await TranslateAndCacheAsync("external_review", externalReviewId.ToString(), res.Value.Content!, targetLang, ct);
		return t is null ? Result.Fail<string>("Translation failed") : Result.Ok(t);
	}

	public async Task<Result<string>> TranslateMediaDescriptionAsync(Guid mediaId, string targetLang, CancellationToken ct)
	{
		var mediaRes = await _uow.MediaRepository.GetOneAsync(m => m.Id == mediaId, "Translations");
		if (mediaRes.IsFailure)
			return Result.Fail<string>("Media not found");

		var en = mediaRes.Value.Translations
			.Where(t => t.LanguageCode == "en"
				&& (t.Status == TranslationStatus.Official || t.Status == TranslationStatus.Approved))
			.Select(t => t.Description)
			.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
			?? mediaRes.Value.Translations
				.Select(t => t.Description)
				.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

		if (string.IsNullOrWhiteSpace(en))
			return Result.Fail<string>("No description to translate");

		var t = await TranslateAndCacheAsync("media_description", mediaId.ToString(), en!, targetLang, ct);
		return t is null ? Result.Fail<string>("Translation failed") : Result.Ok(t);
	}
}
