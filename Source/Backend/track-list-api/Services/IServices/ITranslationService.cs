namespace api.Services.IServices;

public interface ITranslationService
{
	/// <summary>
	///     Look up cached translation OR translate via the configured provider and persist.
	///     Returns the translated text (or the original if already in targetLang / translation fails).
	/// </summary>
	Task<string?> TranslateAndCacheAsync(
		string entityType,
		string entityRefId,
		string sourceText,
		string targetLang,
		CancellationToken ct,
		string sourceLangHint = "en");

	/// <summary>Read-only cache lookup. Never calls the provider. Returns null if not cached.</summary>
	Task<string?> GetCachedAsync(string entityType, string entityRefId, string targetLang, CancellationToken ct);

	/// <summary>Translate an internal review body by id.</summary>
	Task<Result<string>> TranslateReviewAsync(Guid reviewId, string targetLang, CancellationToken ct);

	/// <summary>Translate a comment body by id.</summary>
	Task<Result<string>> TranslateCommentAsync(Guid commentId, string targetLang, CancellationToken ct);

	/// <summary>Translate the cached body of an external review (Letterboxd, Wikipedia).</summary>
	Task<Result<string>> TranslateExternalReviewAsync(Guid externalReviewId, string targetLang, CancellationToken ct);

	/// <summary>Translate the EN TMDB description of a media item; falls back to any available source language.</summary>
	Task<Result<string>> TranslateMediaDescriptionAsync(Guid mediaId, string targetLang, CancellationToken ct);
}
