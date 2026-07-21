namespace api.Services.IServices;

public interface IMediaGetService
{
	Task<Result<(List<Media> Items, int TotalCount)>> GetAllAsync(int pageNumber, int pageSize, MediaType? type, int? yearFrom, int? yearTo, List<int>? genreIds, string? sortBy, CancellationToken ct);
	Task<Result<List<Media>>> SearchAsync(string query, CancellationToken ct);
	Task<Result<Media>> GetByIdAsync(string fullId, CancellationToken ct);
	Task<Result<MediaTranslation>> GetTranslationAsync(string fullId, string languageCode, CancellationToken ct);
}