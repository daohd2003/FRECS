using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ReportService;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Tạo mới một báo cáo
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] ReportDTO reportDto)
        {
            reportDto.Id = Guid.NewGuid();
            reportDto.ReporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _reportService.CreateReportAsync(reportDto);
            return Ok(new { message = "The report has been generated." });
        }

        /// <summary>
        /// Lấy danh sách báo cáo do người dùng tạo (reporter)
        /// </summary>
        [HttpGet("reporter/{reporterId}")]
        public async Task<IActionResult> GetReportsByReporter(Guid reporterId)
        {
            var reports = await _reportService.GetReportsByReporterIdAsync(reporterId);
            return Ok(reports);
        }

        /// <summary>
        /// Lấy danh sách báo cáo mà người dùng là người bị báo cáo (reportee)
        /// </summary>
        [HttpGet("reportee/{reporteeId}")]
        public async Task<IActionResult> GetReportsByReportee(Guid reporteeId)
        {
            var reports = await _reportService.GetReportsByReporteeIdAsync(reporteeId);
            return Ok(reports);
        }

        /// <summary>
        /// Lấy danh sách báo cáo theo trạng thái
        /// </summary>
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetReportsByStatus(ReportStatus status)
        {
            var reports = await _reportService.GetReportsByStatusAsync(status);
            return Ok(reports);
        }

        /// <summary>
        /// Xem chi tiết một báo cáo cụ thể
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(Guid id)
        {
            var report = await _reportService.GetReportByIdAsync(id);
            if (report == null)
                return NotFound();

            return Ok(report);
        }

        /// <summary>
        /// Cập nhật trạng thái xử lý báo cáo (Pending → Reviewed, etc.)
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateReportStatus(Guid id, [FromBody] ReportStatus newStatus)
        {
            await _reportService.UpdateReportStatusAsync(id, newStatus);
            return Ok(new { message = "Cập nhật trạng thái báo cáo thành công." });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("reports/pending")]
        public async Task<IActionResult> GetPendingReports()
        {
            var reports = await _reportService.GetPendingReportsAsync();
            return Ok(new ApiResponse<IEnumerable<ReportDTO>>("Fetched reports", reports));
        }
    }
}
