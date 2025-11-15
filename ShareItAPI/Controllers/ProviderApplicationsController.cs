using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ProviderApplicationServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/provider-applications")]
    public class ProviderApplicationsController : ControllerBase
    {
        private readonly IProviderApplicationService _service;

        public ProviderApplicationsController(IProviderApplicationService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize(Roles = "customer")] // only customers can apply
        public async Task<IActionResult> Apply([FromForm] ProviderApplicationCreateDto dto)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

            var app = await _service.ApplyAsync(userId, dto);
            return Ok(new ApiResponse<object>("Application submitted", new { app.Id, app.Status }));
        }

        [HttpGet("my-pending")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> GetMyPending()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

            var app = await _service.GetMyPendingAsync(userId);
            return Ok(new ApiResponse<object>("Success", app));
        }

        [HttpGet]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAll([FromQuery] ProviderApplicationStatus? status)
        {
            var list = await _service.GetAllApplicationsAsync(status);
            return Ok(new ApiResponse<object>("Success", list));
        }

        [HttpPost("approve/{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Approve(Guid id)
        {
            var staffIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(staffIdClaim)) return Unauthorized();
            var staffId = Guid.Parse(staffIdClaim);

            var ok = await _service.ApproveAsync(staffId, id);
            if (!ok) return BadRequest(new ApiResponse<string>("Unable to approve application", null));
            return Ok(new ApiResponse<string>("Application approved successfully", null));
        }

        [HttpPost("reject/{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApplicationDto dto)
        {
            var staffIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(staffIdClaim)) return Unauthorized();
            var staffId = Guid.Parse(staffIdClaim);

            var ok = await _service.RejectAsync(staffId, id, dto.RejectionReason);
            if (!ok) return BadRequest(new ApiResponse<string>("Unable to reject application", null));
            return Ok(new ApiResponse<string>("Application rejected successfully", null));
        }

        [HttpPut("review")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Review([FromBody] ProviderApplicationReviewDto dto)
        {
            var staffIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(staffIdClaim)) return Unauthorized();
            var staffId = Guid.Parse(staffIdClaim);

            var ok = await _service.ReviewAsync(staffId, dto);
            if (!ok) return BadRequest(new ApiResponse<string>("Unable to review application", null));
            return Ok(new ApiResponse<string>("Application reviewed", null));
        }

        [HttpGet("{id}/images")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetApplicationImages(Guid id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

            try
            {
                var signedUrls = await _service.GetApplicationImagesWithSignedUrlsAsync(id, userId);
                return Ok(new ApiResponse<Dictionary<string, string>>("Success", signedUrls));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        public class RejectApplicationDto
        {
            public string RejectionReason { get; set; } = string.Empty;
        }
    }
}


