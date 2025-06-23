using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.ReportRepositories
{
    public interface IReportRepository : IRepository<Report>
    {
        Task<IEnumerable<Report>> GetReportsByReporterIdAsync(Guid reporterId);
        Task<IEnumerable<Report>> GetReportsByReporteeIdAsync(Guid reporteeId);
        Task<IEnumerable<Report>> GetReportsByStatusAsync(ReportStatus status);
        Task<ReportDTO> GetReportDetailsAsync(Guid reportId);
        Task CreateReportAsync(ReportDTO reportDto);
        Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus);
    }
}
