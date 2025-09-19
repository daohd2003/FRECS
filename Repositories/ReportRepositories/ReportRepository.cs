using AutoMapper;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.ReportRepositories
{
    public class ReportRepository : Repository<Report>, IReportRepository
    {
        public ReportRepository(ShareItDbContext context) : base(context) { }

        public async Task<IEnumerable<Report>> GetReportsByReporterIdAsync(Guid reporterId)
        {
            return await _context.Reports
                .Where(r => r.ReporterId == reporterId)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByReporteeIdAsync(Guid reporteeId)
        {
            return await _context.Reports
                .Where(r => r.ReporteeId == reporteeId)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByStatusAsync(ReportStatus status)
        {
            return await _context.Reports
                .Where(r => r.Status == status)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        // THAY ĐỔI: Sửa lại phương thức này để trả về đối tượng Report đầy đủ thông tin
        // Giúp cho AutoMapper ở tầng Service hoạt động
        public async Task<Report?> GetReportWithDetailsAsync(Guid reportId)
        {
            return await _context.Reports
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .Include(r => r.AssignedAdmin).ThenInclude(u => u.Profile) // Lấy cả thông tin admin được gán
                .FirstOrDefaultAsync(r => r.Id == reportId);
        }

        public async Task CreateReportAsync(ReportDTO dto)
        {
            // Logic tạo Report giữ nguyên
            var report = new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = dto.ReporterId,
                ReporteeId = dto.ReporteeId,
                Subject = dto.Subject,
                Description = dto.Description,
                Status = ReportStatus.open,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                Priority = dto.Priority
            };
            await AddAsync(report);
        }

        public async Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus)
        {
            var report = await GetByIdAsync(reportId);
            if (report == null) return;

            report.Status = newStatus;
            await UpdateAsync(report);
        }
        public IQueryable<Report> GetReportsAsQueryable()
        {
            return _context.Reports
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .Include(r => r.AssignedAdmin).ThenInclude(u => u.Profile)
                .AsQueryable();
        }
    }
}
