using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;

namespace Services.ReportService
{
    public interface IReportService
    {
        Task<IEnumerable<ReportDTO>> GetReportsByReporterIdAsync(Guid reporterId);
        Task<IEnumerable<ReportDTO>> GetReportsByReporteeIdAsync(Guid reporteeId);
        Task<IEnumerable<ReportDTO>> GetReportsByStatusAsync(ReportStatus status);
        Task<ReportDTO> GetReportByIdAsync(Guid reportId);
        Task CreateReportAsync(ReportDTO reportDto);
        Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus);
        Task<IEnumerable<ReportDTO>> GetPendingReportsAsync();
    }
}
