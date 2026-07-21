namespace api.Services.IServices;

public interface IMediaOperationService
{
	Task<Result<MediaDto>> AddAsync(MediaDto dto);
	Task<Result<MediaTranslationDto>> AddTranslationAsync(MediaTranslationDto dto);
	Task<Result<MediaTranslationDto>> SuggestTranslationAsync(MediaTranslationDto dto);

	Task<Result> UpdateAsync(MediaDto dto);
	Task<Result> UpdateTranslationAsync(MediaTranslationDto dto);
	Task<Result> UpdateTranslationStatusAsync(MediaTranslationStatusChangeDto dto);

	Task<Result> DeleteAsync(string id);
	Task<Result> DeleteTranslationAsync(string id);
}