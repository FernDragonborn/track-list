namespace api.Repository.IReposotory;

public interface IReportRepository : IRepository<Report>
{
    // Intentionally hides IRepository<T>.Update(T) — this overload returns the
    // entity directly so callers can chain without unwrapping a Result wrapper.
    new Task<Report> Update(Report report);
}