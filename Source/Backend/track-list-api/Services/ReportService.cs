using api.DTOs;
using api.Services.IServices;
using AutoMapper;

namespace api.Services;

public class ReportService(IUnitOfWork unitOfWork, IMapper mapper) : IReportService
{
	public async Task<Result<ReportDto>> GetByIdAsync(Guid id)
	{
		var reportFetchRes = await unitOfWork.ReportRepository.GetOneAsync(r => r.Id == id);

		if (reportFetchRes.IsFailure)
			return Result.Fail<ReportDto>($"Report with id {id} not found.");

		var dto = mapper.Map<ReportDto>(reportFetchRes.Value);
		dto.TargetNavigation = await ResolveTargetNavAsync(dto.TargetId, dto.TargetType);
		return Result.Ok(dto);
	}

	public async Task<Result<IEnumerable<ReportDto>>> GetAllAsync(ReportStatus? status = null)
	{
		Result<List<Report>> reportsFetchRes;

		if (status.HasValue)
		{
			reportsFetchRes = await unitOfWork.ReportRepository.GetAsync(r => r.Status == status.Value);
		}
		else
		{
			reportsFetchRes = await unitOfWork.ReportRepository.GetAsync();
		}

		var dtos = mapper.Map<IEnumerable<ReportDto>>(reportsFetchRes.Value).ToList();
		foreach (var dto in dtos)
			dto.TargetNavigation = await ResolveTargetNavAsync(dto.TargetId, dto.TargetType);
		return Result.Ok<IEnumerable<ReportDto>>(dtos);
	}

	private async Task<ReportTargetNavigation?> ResolveTargetNavAsync(Guid targetId, ReportableEntityType targetType)
	{
		switch (targetType)
		{
			case ReportableEntityType.Profile:
			{
				var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == targetId);
				if (userRes.IsFailure)
					userRes = await unitOfWork.UserRepository.GetDeletedOneAsync(u => u.Id == targetId);
				if (userRes.IsFailure) return null;

				var user = userRes.Value;
				var isDeleted = user.DeletedAt != null;
				return new ReportTargetNavigation
				{
					Username       = isDeleted ? null : user.Username,
					AuthorUsername = user.Username,
					DisplayName    = user.DisplayName,
					Bio            = isDeleted || string.IsNullOrWhiteSpace(user.Bio) ? null : Truncate(user.Bio, 200),
					IsDeleted      = isDeleted,
				};
			}
			case ReportableEntityType.Review:
			{
				var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == targetId, "User");
				if (reviewRes.IsFailure) return null;
				var review = reviewRes.Value;
				return new ReportTargetNavigation
				{
					MediaId = review.MediaId.ToString(),
					ReviewId = targetId.ToString(),
					AuthorUsername = review.User?.Username,
					Rating = review.Rating,
					ContentExcerpt = string.IsNullOrWhiteSpace(review.Content)
						? null
						: Truncate(StripHtml(review.Content), 300),
				};
			}
			case ReportableEntityType.Comment:
			{
				var commentRes = await unitOfWork.CommentRepository.GetOneAsync(c => c.Id == targetId, "User,Review");
				if (commentRes.IsFailure) return null;
				var comment = commentRes.Value;
				var mediaId = comment.Review?.MediaId.ToString();
				return new ReportTargetNavigation
				{
					MediaId = mediaId,
					ReviewId = comment.ReviewId.ToString(),
					CommentId = targetId.ToString(),
					AuthorUsername = comment.User?.Username,
					ContentExcerpt = string.IsNullOrWhiteSpace(comment.Content)
						? null
						: Truncate(comment.Content, 300),
				};
			}
			case ReportableEntityType.Media:
			{
				var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == targetId, "Translations");
				if (mediaRes.IsFailure) return null;
				var media = mediaRes.Value;
				// media.Translations is an in-memory ICollection materialized via Include
				// ("Translations" eager load in GetOneAsync), so Where(p).FirstOrDefault()
				// is equivalent to FirstOrDefault(p) without changing query semantics.
				var title = media.Translations
					.FirstOrDefault(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)?.Title
					?? media.Translations.FirstOrDefault()?.Title;
				return new ReportTargetNavigation
				{
					MediaId = targetId.ToString(),
					ContentExcerpt = title,
				};
			}
			default:
				return null;
		}
	}

	private static string Truncate(string s, int max) =>
		s.Length <= max ? s : s[..max] + "…";

	private static string StripHtml(string html) =>
		System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();

	public async Task<Result<ReportDto>> CreateAsync(ReportDto reportDto)
	{
		var report = mapper.Map<Report>(reportDto);

		// Бізнес-логіка: новий репорт завжди Pending
		report.Status = ReportStatus.Pending;

		var createdReport = await unitOfWork.ReportRepository.AddAsync(report);
		await unitOfWork.SaveAsync();

		var resultDto = mapper.Map<ReportDto>(createdReport.Value);
		return Result.Ok(resultDto);
	}

	public async Task<Result<ReportDto>> UpdateAsync(ReportDto reportDto)
	{
		var existingReportFetchRes = await unitOfWork.ReportRepository.GetOneAsync(r => r.Id == reportDto.Id);

		if (existingReportFetchRes.IsFailure)
			return Result.Fail<ReportDto>($"Report with id {reportDto.Id} not found.");

		mapper.Map(reportDto, existingReportFetchRes.Value);

		await unitOfWork.ReportRepository.Update(existingReportFetchRes.Value);
		await unitOfWork.SaveAsync();

		// Повертаємо оновлений DTO
		var updatedDto = mapper.Map<ReportDto>(existingReportFetchRes.Value);
		return Result.Ok(updatedDto);
	}

	public async Task<Result> DeleteAsync(Guid id)
	{
		var existingReportFetchRes = await unitOfWork.ReportRepository.GetOneAsync(r => r.Id == id);

		if (existingReportFetchRes.IsFailure)
			return Result.Fail($"Report with id {id} not found.");

		await unitOfWork.ReportRepository.Remove(existingReportFetchRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> ResolveAsync(Guid reportId, Guid moderatorId, ResolveReportRequest request, bool isAdmin)
	{
		if (request.Resolution is not (ReportStatus.ResolvedDeleted or ReportStatus.ResolvedDismissed))
			return Result.Fail("Resolution must be ResolvedDeleted or ResolvedDismissed.");

		if (moderatorId != Guid.Empty)
		{
			var modExists = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == moderatorId);
			if (modExists.IsFailure)
				return Result.Fail("Moderator account no longer exists. Please log in again.");
		}

		var reportRes = await unitOfWork.ReportRepository.GetOneAsync(r => r.Id == reportId);
		if (reportRes.IsFailure)
			return Result.Fail($"Report with id {reportId} not found.");

		var report = reportRes.Value;

		if (report.Status != ReportStatus.Pending)
			return Result.Fail("Report is already resolved.");

		// If deleting content, soft-delete the target
		if (request.Resolution == ReportStatus.ResolvedDeleted)
		{
			if (report.TargetType == ReportableEntityType.Profile && !isAdmin)
				return Result.Fail("Only admins can delete user accounts.");

			var deleteRes = await SoftDeleteTarget(report.TargetId, report.TargetType);
			if (deleteRes.IsFailure)
				return deleteRes;
		}

		report.Status = request.Resolution;
		report.ProcessedByUserId = moderatorId;

		await unitOfWork.ReportRepository.Update(report);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	private async Task<Result> SoftDeleteTarget(Guid targetId, ReportableEntityType targetType)
	{
		switch (targetType)
		{
			case ReportableEntityType.Review:
				var reviewRes = await unitOfWork.ReviewRepository.GetOneAsync(r => r.Id == targetId);
				if (reviewRes.IsFailure) return Result.Ok(); // already deleted — goal achieved
				var removeReviewRes = await unitOfWork.ReviewRepository.Remove(reviewRes.Value);
				return removeReviewRes.IsFailure ? removeReviewRes : Result.Ok();

			case ReportableEntityType.Comment:
				var commentRes = await unitOfWork.CommentRepository.GetOneAsync(c => c.Id == targetId);
				if (commentRes.IsFailure) return Result.Ok(); // already deleted — goal achieved
				var removeCommentRes = await unitOfWork.CommentRepository.Remove(commentRes.Value);
				return removeCommentRes.IsFailure ? removeCommentRes : Result.Ok();

			case ReportableEntityType.Profile:
			{
				var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == targetId);
				if (userRes.IsFailure) return Result.Ok(); // already deleted — goal achieved

				var inbound = await unitOfWork.FollowRepository.GetAsync(f => f.FollowingId == targetId);
				if (inbound.IsSuccess)
					foreach (var f in inbound.Value)
						await unitOfWork.FollowRepository.Remove(f);

				var outbound = await unitOfWork.FollowRepository.GetAsync(f => f.FollowerId == targetId);
				if (outbound.IsSuccess)
					foreach (var f in outbound.Value)
						await unitOfWork.FollowRepository.Remove(f);

				var removeUserRes = await unitOfWork.UserRepository.Remove(userRes.Value);
				return removeUserRes.IsFailure ? removeUserRes : Result.Ok();
			}

			case ReportableEntityType.Media:
			{
				var mediaRes = await unitOfWork.MediaRepository.GetOneAsync(m => m.Id == targetId);
				if (mediaRes.IsFailure) return Result.Ok();
				var media = mediaRes.Value;
				media.DeletedAt = DateTime.UtcNow;
				await unitOfWork.MediaRepository.Update(media);
				return Result.Ok();
			}

			default:
				return Result.Fail($"Unknown target type: {targetType}");
		}
	}
}