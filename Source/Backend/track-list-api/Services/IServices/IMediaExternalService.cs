namespace api.Services.IServices;

public interface IMediaExternalService
{
	Task<Result<Media>> GetByIdAsync(string id, MediaType mediaType, CancellationToken cancellationToken);
	Task<Result<List<Media>>> GetByNameAsync(string query, CancellationToken cancellationToken);

	Task<Result<MediaTranslation>> GetTranslationAsync(string id, MediaType mediaType, string languageCode,
		CancellationToken ct);
}