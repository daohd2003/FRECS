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
        public async Task<IActionResult> Apply([FromBody] ProviderApplicationCreateDto dto)
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
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAll([FromQuery] ProviderApplicationStatus? status)
        {
            var list = await _service.GetByStatusAsync(status ?? ProviderApplicationStatus.pending);
            return Ok(new ApiResponse<object>("Success", list));
        }

        [HttpPut("review")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Review([FromBody] ProviderApplicationReviewDto dto)
        {
            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminIdClaim)) return Unauthorized();
            var adminId = Guid.Parse(adminIdClaim);

            var ok = await _service.ReviewAsync(adminId, dto);
            if (!ok) return BadRequest(new ApiResponse<string>("Unable to review application", null));
            return Ok(new ApiResponse<string>("Application reviewed", null));
        }
    }
}


