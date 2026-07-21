using api.DbContext;

namespace api.Repository;

public class ReportRepository : Repository<Report>, IReportRepository
{
    private readonly TrackListDbContext _db;

    public ReportRepository(TrackListDbContext db) : base(db)
    {
        _db = db;
    }

    public new Task<Report> Update(Report report)
    {
        _db.Reports.Update(report);
        return Task.FromResult(report);
    }
}