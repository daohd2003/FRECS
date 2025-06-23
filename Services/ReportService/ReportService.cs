using AutoMapper;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using Repositories.ReportRepositories;

namespace Services.ReportService
{
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepository;
        private readonly IMapper _mapper;

        public ReportService(IReportRepository reportRepository, IMapper mapper)
        {
            _reportRepository = reportRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ReportDTO>> GetReportsByReporterIdAsync(Guid reporterId)
        {
            var reports = await _reportRepository.GetReportsByReporterIdAsync(reporterId);
            return _mapper.Map<IEnumerable<ReportDTO>>(reports);
        }

        public async Task<IEnumerable<ReportDTO>> GetReportsByReporteeIdAsync(Guid reporteeId)
        {
            var reports = await _reportRepository.GetReportsByReporteeIdAsync(reporteeId);
            return _mapper.Map<IEnumerable<ReportDTO>>(reports);
        }

        public async Task<IEnumerable<ReportDTO>> GetReportsByStatusAsync(ReportStatus status)
        {
            var reports = await _reportRepository.GetReportsByStatusAsync(status);
            return _mapper.Map<IEnumerable<ReportDTO>>(reports);
        }

        public async Task<ReportDTO> GetReportByIdAsync(Guid reportId)
        {
            return await _reportRepository.GetReportDetailsAsync(reportId);
        }

        public async Task CreateReportAsync(ReportDTO reportDto)
        {
            await _reportRepository.CreateReportAsync(reportDto);
        }

        public async Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus)
        {
            await _reportRepository.UpdateReportStatusAsync(reportId, newStatus);
        }
        public async Task<IEnumerable<ReportDTO>> GetPendingReportsAsync()
        {
            var reports = await _reportRepository.GetAllAsync();

            var filtered = reports
                .Where(r => r.Status == ReportStatus.open || r.Status == ReportStatus.in_progress)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return _mapper.Map<IEnumerable<ReportDTO>>(filtered);
        }
    }
}
