using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Services.ReportService;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/report")] // Sửa lại route cho nhất quán
    [ApiController]
    [Authorize] // Bảo vệ toàn bộ controller
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Người dùng tạo mới một báo cáo
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer,provider")] // Chỉ customer hoặc provider mới được tạo report
        public async Task<IActionResult> CreateReport([FromBody] ReportDTO reportDto)
        {
            var reporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            reportDto.ReporterId = reporterId;

            await _reportService.CreateReportAsync(reportDto);
            return Ok(new ApiResponse<string>("Report submitted successfully. We will review it shortly.", null));
        }

        

        

        /// <summary>
        /// [ADMIN] Xem chi tiết một báo cáo cụ thể
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetReportDetails(Guid id)
        {
            var report = await _reportService.GetReportDetailsAsync(id);
            if (report == null) return NotFound(new ApiResponse<string>("Report not found.", null));
            return Ok(new ApiResponse<ReportViewModel>("Report details fetched.", report));
        }

        /// <summary>
        /// [ADMIN] Lấy danh sách tất cả admin để gán việc
        /// </summary>
        [HttpGet("admins")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllAdmins()
        {
            var currentAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var admins = await _reportService.GetAllAdminsAsync(currentAdminId);
            return Ok(new ApiResponse<IEnumerable<AdminViewModel>>("Fetched list of admins.", admins));
        }

        /// <summary>
        /// [ADMIN] Nhận một báo cáo để xử lý
        /// </summary>
        [HttpPost("{id}/take")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> TakeReport(Guid id)
        {
            var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var (success, message) = await _reportService.TakeReportAsync(id, adminId);
            if (!success) return BadRequest(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN] Gán một báo cáo cho một admin khác
        /// </summary>
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AssignReport(Guid id, [FromBody] AssignReportRequest request)
        {
            var (success, message) = await _reportService.AssignReportAsync(id, request.NewAdminId);
            if (!success) return NotFound(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN] Cập nhật trạng thái của một báo cáo
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReportStatusRequest request)
        {
            var (success, message) = await _reportService.UpdateStatusAsync(id, request.NewStatus);
            if (!success) return NotFound(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN] Gửi phản hồi cho người dùng và cập nhật trạng thái
        /// </summary>
        [HttpPost("{id}/respond")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RespondToReport(Guid id, [FromBody] RespondToReportRequest request)
        {
            var (success, message) = await _reportService.AddResponseAsync(id, request.ResponseMessage, request.NewStatus);
            if (!success) return NotFound(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }


        //[HttpGet("unassigned")]
        //[Authorize(Roles = "admin")]
        //public async Task<ActionResult<List<ReportViewModel>>> GetUnassignedReports()
        //{
        //    var reports = await _reportService.GetUnassignedReportsAsync();
        //    return Ok(await reports.ToListAsync());
        //}


        //[HttpGet("mytasks")]
        //[Authorize(Roles = "admin")]
        //public async Task<ActionResult<List<ReportViewModel>>> GetMyTasks()
        //{
        //    var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        //    var reportTasks = await _reportService.GetReportsByAdminIdAsync(adminId);
        //    return Ok(await reportTasks.ToListAsync());
        //}

    }
}
