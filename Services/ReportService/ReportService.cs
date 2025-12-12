using AutoMapper;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Repositories.EmailRepositories;
using Repositories.ReportRepositories;
using Repositories.UserRepositories;
using Services.EmailServices;

namespace Services.ReportService
{
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly IHubContext<ReportHub> _hubContext;
        private readonly IEmailRepository _emailRepository;

        public ReportService(
            IReportRepository reportRepository,
            IUserRepository userRepository,
            IMapper mapper,
            IHubContext<ReportHub> hubContext,
            IEmailRepository emailRepository)
        {
            _reportRepository = reportRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _hubContext = hubContext;
            _emailRepository = emailRepository;
        }

        public async Task CreateReportAsync(ReportDTO reportDto)
        {
            await _reportRepository.CreateReportAsync(reportDto);
            // Sau khi tạo, có thể bắn SignalR event cho admin
            var newReports = await GetUnassignedReportsAsync();
            await _hubContext.Clients.All.SendAsync("NewReportReceived", newReports);
        }

        public async Task<ReportViewModel?> GetReportDetailsAsync(Guid reportId)
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId);
            return _mapper.Map<ReportViewModel>(report);
        }

        public Task<IQueryable<ReportViewModel>> GetUnassignedReportsAsync()
        {
            var query = _reportRepository.GetReportsAsQueryable()
                .Where(r => r.AssignedAdminId == null && r.Status == ReportStatus.open);

            var result = _mapper.ProjectTo<ReportViewModel>(query);

            return Task.FromResult(result);
        }

        public Task<IQueryable<ReportViewModel>> GetReportsByAdminIdAsync(Guid adminId)
        {
            var query = _reportRepository.GetReportsAsQueryable()
                .Where(r => r.AssignedAdminId == adminId);

            var result = _mapper.ProjectTo<ReportViewModel>(query);

            return Task.FromResult(result);
        }

        public async Task<IEnumerable<AdminViewModel>> GetAllAdminsAsync(Guid? currentAdminIdToExclude)
        {
            // Lấy cả admin và staff
            var admins = await _userRepository.GetByCondition(u => (u.Role == UserRole.admin || u.Role == UserRole.staff) && u.Id != currentAdminIdToExclude);
            // Logic đếm task có thể thêm vào đây
            return _mapper.Map<IEnumerable<AdminViewModel>>(admins);
        }

        public async Task<(bool Success, string Message)> TakeReportAsync(Guid reportId, Guid adminId)
        {
            var adminTasks = await _reportRepository.GetByCondition(r => r.AssignedAdminId == adminId);
            int inProgressCount = adminTasks.Count(t => t.Status == ReportStatus.in_progress);
            int awaitingCount = adminTasks.Count(t => t.Status == ReportStatus.awaiting_customer_response);

            if (inProgressCount >= 2)
            {
                if (!(adminTasks.Count() == 2 && awaitingCount == 2))
                {
                    return (false, "Task limit reached. You can only have up to 2 'In Progress' tasks.");
                }
            }

            var report = await _reportRepository.GetReportWithDetailsAsync(reportId);
            if (report == null || report.AssignedAdminId != null)
            {
                return (false, "Report is no longer available or has been taken.");
            }

            report.AssignedAdminId = adminId;
            report.Status = ReportStatus.in_progress;
            await _reportRepository.UpdateAsync(report);

            var reportViewModel = await GetReportDetailsAsync(reportId);
            await _hubContext.Clients.All.SendAsync("ReportAssigned", reportViewModel);
            return (true, "Task taken successfully.");
        }

        public async Task<(bool Success, string Message)> AssignReportAsync(Guid reportId, Guid newAdminId)
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null) return (false, "Report not found.");

            report.AssignedAdminId = newAdminId;
            await _reportRepository.UpdateAsync(report);

            var reportViewModel = await GetReportDetailsAsync(reportId);
            await _hubContext.Clients.All.SendAsync("ReportAssigned", reportViewModel);
            return (true, "Report assigned successfully.");
        }

        public async Task<(bool Success, string Message)> AddResponseAsync(Guid reportId, string responseMessage, ReportStatus newStatus)
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId);
            if (report == null) return (false, "Report not found.");

            // Only allow response when status is resolved
            if (newStatus != ReportStatus.resolved)
            {
                return (false, "Response can only be sent when resolving the report.");
            }

            report.AdminResponse = responseMessage;
            report.Status = newStatus;
            await _reportRepository.UpdateAsync(report);

            // Only send email when status is resolved
            string subject = $"Re: Your report about '{report.Subject}'";
            string body = $"An admin has responded to your report: <br/><p><i>{responseMessage}</i></p>";
            await _emailRepository.SendEmailAsync(report.Reporter.Email, subject, body);

            var reportViewModel = _mapper.Map<ReportViewModel>(report);
            await _hubContext.Clients.All.SendAsync("ReportStatusChanged", reportViewModel);
            return (true, "Report resolved and response sent to reporter.");
        }

        public async Task<(bool Success, string Message)> UpdateStatusAsync(Guid reportId, ReportStatus newStatus)
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null) return (false, "Report not found.");

            report.Status = newStatus;
            await _reportRepository.UpdateAsync(report);

            var reportViewModel = await GetReportDetailsAsync(reportId);
            await _hubContext.Clients.All.SendAsync("ReportStatusChanged", reportViewModel);
            return (true, "Status updated successfully.");
        }

        public IQueryable<ReportViewModel> GetUnassignedReports()
        {
            return _reportRepository.GetReportsAsQueryable()
                .Where(r => r.AssignedAdminId == null && r.Status == ReportStatus.open)
                .AsNoTracking()
                .Select(r => new ReportViewModel
                {
                    Id = r.Id,
                    ReporterId = r.ReporterId,
                    ReporteeId = r.ReporteeId,
                    Subject = r.Subject,
                    Description = r.Description,
                    Status = r.Status.ToString(),
                    Priority = r.Priority,
                    DateCreated = r.CreatedAt,
                    ReporterName = r.Reporter != null && r.Reporter.Profile != null ? r.Reporter.Profile.FullName : "Unknown",
                    ReporterEmail = r.Reporter != null ? r.Reporter.Email : "Unknown",
                    ReporterAvatar = r.Reporter != null && r.Reporter.Profile != null ? r.Reporter.Profile.ProfilePictureUrl : null,
                    ReporteeName = r.Reportee != null && r.Reportee.Profile != null ? r.Reportee.Profile.FullName : "Unknown",
                    ReporteeEmail = r.Reportee != null ? r.Reportee.Email : "Unknown",
                    ReporteeAvatar = r.Reportee != null && r.Reportee.Profile != null ? r.Reportee.Profile.ProfilePictureUrl : null,
                    AssignedAdminId = r.AssignedAdminId,
                    AssignedAdminName = r.AssignedAdmin != null && r.AssignedAdmin.Profile != null ? r.AssignedAdmin.Profile.FullName : null,
                    AdminResponse = r.AdminResponse,
                    OrderId = r.OrderId,
                    OrderItemId = r.OrderItemId,
                    ReportType = r.ReportType,
                    OrderCode = r.Order != null ? "ORD-" + r.Order.Id.ToString().Substring(0, 8).ToUpper() : null,
                    EvidenceImages = null,
                    OrderProducts = null,
                    ReportedProduct = null
                });
        }

        public IQueryable<ReportViewModel> GetReportsByAdminId(Guid adminId)
        {
            return _reportRepository.GetReportsAsQueryable()
                .Where(r => r.AssignedAdminId == adminId)
                .AsNoTracking()
                .Select(r => new ReportViewModel
                {
                    Id = r.Id,
                    ReporterId = r.ReporterId,
                    ReporteeId = r.ReporteeId,
                    Subject = r.Subject,
                    Description = r.Description,
                    Status = r.Status.ToString(),
                    Priority = r.Priority,
                    DateCreated = r.CreatedAt,
                    ReporterName = r.Reporter != null && r.Reporter.Profile != null ? r.Reporter.Profile.FullName : "Unknown",
                    ReporterEmail = r.Reporter != null ? r.Reporter.Email : "Unknown",
                    ReporterAvatar = r.Reporter != null && r.Reporter.Profile != null ? r.Reporter.Profile.ProfilePictureUrl : null,
                    ReporteeName = r.Reportee != null && r.Reportee.Profile != null ? r.Reportee.Profile.FullName : "Unknown",
                    ReporteeEmail = r.Reportee != null ? r.Reportee.Email : "Unknown",
                    ReporteeAvatar = r.Reportee != null && r.Reportee.Profile != null ? r.Reportee.Profile.ProfilePictureUrl : null,
                    AssignedAdminId = r.AssignedAdminId,
                    AssignedAdminName = r.AssignedAdmin != null && r.AssignedAdmin.Profile != null ? r.AssignedAdmin.Profile.FullName : null,
                    AdminResponse = r.AdminResponse,
                    OrderId = r.OrderId,
                    OrderItemId = r.OrderItemId,
                    ReportType = r.ReportType,
                    OrderCode = r.Order != null ? "ORD-" + r.Order.Id.ToString().Substring(0, 8).ToUpper() : null,
                    EvidenceImages = null,
                    OrderProducts = null,
                    ReportedProduct = null
                });
        }

    }
}