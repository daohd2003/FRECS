using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.ReportRepositories
{
    public interface IReportRepository : IRepository<Report>
    {
        // Các phương thức cũ, giữ lại vì có thể đang được dùng ở nơi khác
        Task<IEnumerable<Report>> GetReportsByReporterIdAsync(Guid reporterId);
        Task<IEnumerable<Report>> GetReportsByReporteeIdAsync(Guid reporteeId);
        Task<IEnumerable<Report>> GetReportsByStatusAsync(ReportStatus status);

        // THAY ĐỔI: Sửa phương thức này để trả về Model thay vì DTO
        Task<Report?> GetReportWithDetailsAsync(Guid reportId);

        // Các phương thức mới
        Task CreateReportAsync(ReportDTO dto);
        Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus);
        IQueryable<Report> GetReportsAsQueryable();
    }
}