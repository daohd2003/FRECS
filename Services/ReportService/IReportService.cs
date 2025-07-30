using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;

namespace Services.ReportService
{
    public interface IReportService
    {
        Task CreateReportAsync(ReportDTO reportDto);
        Task<ReportViewModel?> GetReportDetailsAsync(Guid reportId);
        Task<IQueryable<ReportViewModel>> GetUnassignedReportsAsync();
        Task<IQueryable<ReportViewModel>> GetReportsByAdminIdAsync(Guid adminId);
        Task<IEnumerable<AdminViewModel>> GetAllAdminsAsync(Guid? currentAdminIdToExclude);
        Task<(bool Success, string Message)> TakeReportAsync(Guid reportId, Guid adminId);
        Task<(bool Success, string Message)> AssignReportAsync(Guid reportId, Guid newAdminId);
        Task<(bool Success, string Message)> AddResponseAsync(Guid reportId, string responseMessage, ReportStatus newStatus);
        Task<(bool Success, string Message)> UpdateStatusAsync(Guid reportId, ReportStatus newStatus);

        //odata
        IQueryable<ReportViewModel> GetUnassignedReports();
        IQueryable<ReportViewModel> GetReportsByAdminId(Guid adminId);

    }
}
