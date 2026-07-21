using api.DTOs;

namespace api.Services.IServices;

public interface IReportService
{
	Task<Result<ReportDto>> GetByIdAsync(Guid id);
	Task<Result<IEnumerable<ReportDto>>> GetAllAsync(ReportStatus? status = null);
	Task<Result<ReportDto>> CreateAsync(ReportDto reportDto);
	Task<Result<ReportDto>> UpdateAsync(ReportDto reportDto);
	Task<Result> DeleteAsync(Guid id);
	Task<Result> ResolveAsync(Guid reportId, Guid moderatorId, ResolveReportRequest request, bool isAdmin);
}