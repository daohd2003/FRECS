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
        /*private readonly YourDbContext _context;

        public ReportRepository(YourDbContext context) : base(context)
        {
            _context = context;
        }*/
        public ReportRepository(ShareItDbContext context, IMapper mapper) : base(context) { }

        public async Task<IEnumerable<Report>> GetReportsByReporterIdAsync(Guid reporterId)
        {
            return await _context.Reports
                .Where(r => r.ReporterId == reporterId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByReporteeIdAsync(Guid reporteeId)
        {
            return await _context.Reports
                .Where(r => r.ReporteeId == reporteeId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByStatusAsync(ReportStatus status)
        {
            return await _context.Reports
                .Where(r => r.Status == status)
                .ToListAsync();
        }

        public async Task<ReportDTO> GetReportDetailsAsync(Guid reportId)
        {
            var report = await _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.Reportee)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return null;

            return new ReportDTO
            {
                Id = report.Id,
                ReporterId = report.ReporterId,
                ReporteeId = report.ReporteeId,
                Subject = report.Subject,
                Description = report.Description,
                Status = report.Status,
                CreatedAt = report.CreatedAt
            };
        }

        public async Task CreateReportAsync(ReportDTO dto)
        {
            var report = new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = dto.ReporterId,
                ReporteeId = dto.ReporteeId,
                Subject = dto.Subject,
                Description = dto.Description,
                Status = ReportStatus.open,
                CreatedAt = DateTime.Now
            };

            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null) return;

            report.Status = newStatus;
            _context.Reports.Update(report);
            await _context.SaveChangesAsync();
        }
    }
}
