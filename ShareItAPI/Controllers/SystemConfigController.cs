using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.SystemConfigDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.SystemConfigServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemConfigController : ControllerBase
    {
        private readonly ISystemConfigService _systemConfigService;

        public SystemConfigController(ISystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID claim not found.");
            }
            return Guid.Parse(userIdClaim);
        }

        /// <summary>
        /// Get current commission rates
        /// </summary>
        [HttpGet("commission-rates")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetCommissionRates()
        {
            try
            {
                var commissionRates = await _systemConfigService.GetCommissionRatesAsync();
                return Ok(new ApiResponse<CommissionRatesDto>("Commission rates retrieved successfully", commissionRates));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving commission rates", ex.Message));
            }
        }

        /// <summary>
        /// Update commission rates (Admin only)
        /// </summary>
        [HttpPut("commission-rates")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateCommissionRates([FromBody] UpdateCommissionRatesRequest request)
        {
            try
            {
                if (request.RentalCommissionRate < 0 || request.RentalCommissionRate > 100)
                {
                    return BadRequest(new ApiResponse<string>("Rental commission rate must be between 0 and 100", null));
                }

                if (request.PurchaseCommissionRate < 0 || request.PurchaseCommissionRate > 100)
                {
                    return BadRequest(new ApiResponse<string>("Purchase commission rate must be between 0 and 100", null));
                }

                var adminId = GetUserId();
                var success = await _systemConfigService.UpdateCommissionRatesAsync(
                    request.RentalCommissionRate,
                    request.PurchaseCommissionRate,
                    adminId
                );

                if (success)
                {
                    var updatedRates = await _systemConfigService.GetCommissionRatesAsync();
                    return Ok(new ApiResponse<CommissionRatesDto>("Commission rates updated successfully", updatedRates));
                }

                return StatusCode(500, new ApiResponse<string>("Failed to update commission rates", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while updating commission rates", ex.Message));
            }
        }
    }

    public class UpdateCommissionRatesRequest
    {
        public decimal RentalCommissionRate { get; set; }
        public decimal PurchaseCommissionRate { get; set; }
    }
}
