using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Services.ReportService;
using Services.CloudServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/report")] // Sửa lại route cho nhất quán
    [ApiController]
    [Authorize] // Bảo vệ toàn bộ controller
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(IReportService reportService, ICloudinaryService cloudinaryService, ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        /// <summary>
        /// Upload ảnh evidence cho report
        /// </summary>
        [HttpPost("upload-evidence")]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> UploadEvidenceImages([FromForm] List<IFormFile> files)
        {
            try
            {
                if (files == null || !files.Any())
                {
                    return BadRequest(new { message = "No files provided" });
                }

                if (files.Count > 5)
                {
                    return BadRequest(new { message = "Maximum 5 images allowed" });
                }

                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var uploadResults = new List<string>();

                foreach (var file in files)
                {
                    // Validate file
                    if (file.Length == 0) continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var extension = Path.GetExtension(file.FileName)?.ToLower() ?? "";
                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { message = $"Invalid file type: {file.FileName}. Only JPG, JPEG, PNG, GIF, WEBP allowed." });
                    }

                    if (file.Length > 5 * 1024 * 1024) // 5MB
                    {
                        return BadRequest(new { message = $"File {file.FileName} exceeds 5MB limit" });
                    }

                    // Upload to Cloudinary
                    var result = await _cloudinaryService.UploadSingleImageAsync(file, userId, "ShareIt", "evidence");
                    uploadResults.Add(result.ImageUrl);
                }

                return Ok(new { 
                    success = true, 
                    message = "Images uploaded successfully", 
                    urls = uploadResults 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading evidence images for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return StatusCode(500, new { message = "Failed to upload images", error = ex.Message });
            }
        }

        /// <summary>
        /// Feature: Report issues to staff
        /// The user submits a formal support ticket to staff regarding system bugs, order problems, or other issues.
        /// Người dùng tạo mới một báo cáo
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer,provider")] // Chỉ customer hoặc provider mới được tạo report
        public async Task<IActionResult> CreateReport([FromBody] ReportDTO reportDto)
        {
            try
            {
                var reporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                reportDto.ReporterId = reporterId;

                // Validation: Nếu report về OrderItem thì phải có OrderItemId
                if (reportDto.ReportType == ReportType.OrderItem)
                {
                    if (!reportDto.OrderItemId.HasValue)
                    {
                        return BadRequest(new ApiResponse<string>("OrderItemId is required when reporting about a specific product.", null));
                    }
                }
                else if (reportDto.ReportType == ReportType.Order)
                {
                    // Report về toàn bộ order - cần OrderId, không cần OrderItemId
                    if (!reportDto.OrderId.HasValue)
                    {
                        return BadRequest(new ApiResponse<string>("OrderId is required when reporting about an order.", null));
                    }
                    reportDto.OrderItemId = null;
                }
                else if (reportDto.ReportType == ReportType.General)
                {
                    // General reports không cần OrderId hoặc OrderItemId
                    reportDto.OrderId = null;
                    reportDto.OrderItemId = null;
                }

                await _reportService.CreateReportAsync(reportDto);
                return Ok(new ApiResponse<string>("Report submitted successfully. We will review it shortly.", null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        

        

        /// <summary>
        /// [ADMIN/STAFF] Xem chi tiết một báo cáo cụ thể
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetReportDetails(Guid id)
        {
            var report = await _reportService.GetReportDetailsAsync(id);
            if (report == null) return NotFound(new ApiResponse<string>("Report not found.", null));
            return Ok(new ApiResponse<ReportViewModel>("Report details fetched.", report));
        }

        /// <summary>
        /// [ADMIN/STAFF] Lấy danh sách tất cả admin để gán việc
        /// </summary>
        [HttpGet("admins")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAllAdmins()
        {
            var currentAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var admins = await _reportService.GetAllAdminsAsync(currentAdminId);
            return Ok(new ApiResponse<IEnumerable<AdminViewModel>>("Fetched list of admins.", admins));
        }

        /// <summary>
        /// [ADMIN/STAFF] Nhận một báo cáo để xử lý
        /// </summary>
        [HttpPost("{id}/take")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> TakeReport(Guid id)
        {
            var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var (success, message) = await _reportService.TakeReportAsync(id, adminId);
            if (!success) return BadRequest(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN/STAFF] Gán một báo cáo cho một admin khác
        /// </summary>
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AssignReport(Guid id, [FromBody] AssignReportRequest request)
        {
            var (success, message) = await _reportService.AssignReportAsync(id, request.NewAdminId);
            if (!success) return NotFound(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN/STAFF] Cập nhật trạng thái của một báo cáo
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReportStatusRequest request)
        {
            var (success, message) = await _reportService.UpdateStatusAsync(id, request.NewStatus);
            if (!success) return NotFound(new ApiResponse<string>(message, null));
            return Ok(new ApiResponse<string>(message, null));
        }

        /// <summary>
        /// [ADMIN/STAFF] Gửi phản hồi cho người dùng và cập nhật trạng thái
        /// </summary>
        [HttpPost("{id}/respond")]
        [Authorize(Roles = "admin,staff")]
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
