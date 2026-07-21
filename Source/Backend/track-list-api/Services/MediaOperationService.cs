using api.Services.IServices;
using api.Utils;
using AutoMapper;

namespace api.Services;

public class MediaOperationService(IUnitOfWork unitOfWork, IMapper mapper) : IMediaOperationService
{
	public async Task<Result<MediaDto>> AddAsync(MediaDto dto)
	{
		// Перевірка на дублікати
		var existingMediaFetchRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.ExternalApiId == dto.ExternalApiId && m.Type == dto.Type);
		if (existingMediaFetchRes.IsSuccess)
			return Result.Fail<MediaDto>($"{nameof(Media)} with ExternalId '{dto.ExternalApiId}' already exists.");

		var mediaEntity = mapper.Map<Media>(dto);
		var createdMedia = await unitOfWork.MediaRepository.AddAsync(mediaEntity);

		if (createdMedia.IsFailure)
			return Result.Fail<MediaDto>(createdMedia.Error);

		await unitOfWork.SaveAsync();

		// Мапінг назад Entity -> DTO (щоб повернути ID та інші генеровані поля)
		var resultDto = mapper.Map<MediaDto>(createdMedia.Value);

		return Result.Ok(resultDto);
	}

	public async Task<Result<MediaTranslationDto>> AddTranslationAsync(MediaTranslationDto dto)
	{
		if (dto.MediaId == Guid.Empty)
			return Result.Fail<MediaTranslationDto>($"Invalid {nameof(Media)} ID associated with {nameof(MediaTranslation)}.");

		var existingTranslationFetchRes = await unitOfWork.MediaTranslationRepository
			.GetOneAsync(t => t.MediaId == dto.MediaId
			                  && t.LanguageCode == dto.LanguageCode
			);
		if (existingTranslationFetchRes.IsSuccess)
			return Result.Fail<MediaTranslationDto>($"{nameof(MediaTranslation)} for language \'{dto.LanguageCode}\' already exists.");

		var translationEntity = mapper.Map<MediaTranslation>(dto);
		translationEntity.Status = TranslationStatus.Pending;

		var createdTranslation = await unitOfWork.MediaTranslationRepository.AddAsync(translationEntity);
		await unitOfWork.SaveAsync();

		var resultDto = mapper.Map<MediaTranslationDto>(createdTranslation);

		return Result.Ok(resultDto);
	}

	public async Task<Result<MediaTranslationDto>> SuggestTranslationAsync(MediaTranslationDto dto)
	{
		if (dto.MediaId == Guid.Empty)
			return Result.Fail<MediaTranslationDto>($"Invalid {nameof(Media)} ID.");

		if (string.IsNullOrWhiteSpace(dto.Title))
			return Result.Fail<MediaTranslationDto>("Title is required.");

		var translationEntity = mapper.Map<MediaTranslation>(dto);
		translationEntity.Status = TranslationStatus.Pending;

		var created = await unitOfWork.MediaTranslationRepository.AddAsync(translationEntity);
		if (created.IsFailure)
			return Result.Fail<MediaTranslationDto>(created.Error);

		await unitOfWork.SaveAsync();
		return Result.Ok(mapper.Map<MediaTranslationDto>(created.Value));
	}

	public async Task<Result> UpdateAsync(MediaDto dto)
	{
		if (dto.Id == Guid.Empty)
			return Result.Fail("Invalid Media ID.");

		var mediaFetchRes = await unitOfWork.MediaRepository.GetOneAsync(u => u.Id == dto.Id);

		if (mediaFetchRes.IsFailure)
			return Result.Fail($"{nameof(MediaTranslation)} with id \'{dto.Id}\' not found");

		mapper.Map(dto, mediaFetchRes);

		await unitOfWork.MediaRepository.Update(mediaFetchRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> UpdateTranslationAsync(MediaTranslationDto dto)
	{
		if (dto.Id == Guid.Empty)
			return Result.Fail("Invalid Translation ID.");

		var translationFetchRes = await unitOfWork.MediaTranslationRepository.GetOneAsync(u => u.Id == dto.Id);

		if (translationFetchRes.IsFailure)
			return Result.Fail($"{nameof(MediaTranslation)} with id \'{dto.Id}\' not found");

		mapper.Map(dto, translationFetchRes);
		translationFetchRes.Value.Status = TranslationStatus.Pending;

		await unitOfWork.MediaTranslationRepository.Update(translationFetchRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> UpdateTranslationStatusAsync(MediaTranslationStatusChangeDto dto)
	{
		if (dto.TranslationId == Guid.Empty)
			return Result.Fail($"Invalid {nameof(MediaTranslation)} ID.");

		if (dto.ProcessedByUserId != Guid.Empty)
		{
			var userExists = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == dto.ProcessedByUserId);
			if (userExists.IsFailure)
				return Result.Fail("Moderator account no longer exists. Please log in again.");
		}

		var translationFetchRes = await unitOfWork.MediaTranslationRepository.GetOneAsync(u => u.Id == dto.TranslationId);

		if (translationFetchRes.IsFailure)
			return Result.Fail($"{nameof(MediaTranslation)} with id \'{dto.TranslationId}\' not found");

		translationFetchRes.Value.Status = dto.Status;
		translationFetchRes.Value.ProcessedByUserId = dto.ProcessedByUserId;

		await unitOfWork.MediaTranslationRepository.Update(translationFetchRes.Value);
		await unitOfWork.SaveAsync();

		return Result.Ok();
	}

	public async Task<Result> DeleteAsync(string id)
	{
		var guidParseRes = GuidParser.TryParseGuid(id);
		if (guidParseRes.IsFailure)
			return Result.Fail(guidParseRes.Error);

		var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == guidParseRes.Value);

		if (mediaRes.IsFailure)
			return Result.Fail($"{nameof(Media)} with id \'{id}\' not found.");

		await unitOfWork.MediaRepository.Remove(mediaRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> DeleteTranslationAsync(string id)
	{
		var guidParseRes = GuidParser.TryParseGuid(id);
		if (guidParseRes.IsFailure)
			return Result.Fail(guidParseRes.Error);

		var translationFetchRes = await unitOfWork.MediaTranslationRepository.GetOneAsync(t => t.Id == guidParseRes.Value);

		if (translationFetchRes.IsFailure)
			return Result.Fail($"{nameof(MediaTranslation)} with \'{id}\' not found.");

		await unitOfWork.MediaTranslationRepository.Remove(translationFetchRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}
}
