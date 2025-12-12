using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DepositDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DepositServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/deposits")]
    [Authorize(Roles = "customer,provider")]
    public class DepositController : ControllerBase
    {
        private readonly IDepositService _depositService;

        public DepositController(IDepositService depositService)
        {
            _depositService = depositService;
        }

        /// <summary>
        /// Get deposit statistics for customer
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDepositStats()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var stats = await _depositService.GetDepositStatsAsync(customerId);
            
            return Ok(new ApiResponse<DepositStatsDto>("Deposit statistics retrieved", stats));
        }

        /// <summary>
        /// Get deposit history for customer
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetDepositHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var history = await _depositService.GetDepositHistoryAsync(customerId);
            
            return Ok(new ApiResponse<List<DepositHistoryDto>>("Deposit history retrieved", history));
        }
    }
}

