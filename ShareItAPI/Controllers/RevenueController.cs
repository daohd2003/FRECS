using BusinessObject.DTOs.RevenueDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.RevenueServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RevenueController : ControllerBase
    {
        private readonly IRevenueService _revenueService;
        private readonly ILogger<RevenueController> _logger;

        public RevenueController(IRevenueService revenueService, ILogger<RevenueController> logger)
        {
            _revenueService = revenueService;
            _logger = logger;
        }

        /// <summary>
        /// Get revenue statistics for the current user (Provider)
        /// NOTE: Front-end should handle 401 Unauthorized responses by redirecting to the login page.
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "provider")]
        public async Task<ActionResult<RevenueStatsDto>> GetRevenueStats([FromQuery] string period = "month", [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var stats = await _revenueService.GetRevenueStatsAsync(userId, period, startDate, endDate);
                return Ok(stats);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid date range provided by user");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revenue stats for user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Admin endpoint: Get revenue stats for any provider
        /// </summary>
        [HttpGet("provider/{providerId}/stats")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<RevenueStatsDto>> GetProviderRevenueStats(Guid providerId, [FromQuery] string period = "month", [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var stats = await _revenueService.GetRevenueStatsAsync(providerId, period, startDate, endDate);
                return Ok(stats);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid date range provided for provider {ProviderId}", providerId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revenue stats for provider {ProviderId}", providerId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("payout-summary")]
        public async Task<ActionResult<PayoutSummaryDto>> GetPayoutSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var summary = await _revenueService.GetPayoutSummaryAsync(userId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payout summary for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("payout-history")]
        public async Task<ActionResult<List<PayoutHistoryDto>>> GetPayoutHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var history = await _revenueService.GetPayoutHistoryAsync(userId, page, pageSize);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payout history for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("bank-accounts")]
        public async Task<ActionResult<List<BankAccountDto>>> GetBankAccounts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var accounts = await _revenueService.GetBankAccountsAsync(userId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bank accounts for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("bank-accounts")]
        public async Task<ActionResult<BankAccountDto>> CreateBankAccount([FromBody] CreateBankAccountDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var account = await _revenueService.CreateBankAccountAsync(userId, dto);
                return CreatedAtAction(nameof(GetBankAccounts), account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("bank-accounts/{accountId}")]
        public async Task<ActionResult> UpdateBankAccount(Guid accountId, [FromBody] CreateBankAccountDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _revenueService.UpdateBankAccountAsync(userId, accountId, dto);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank account for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("bank-accounts/{accountId}")]
        public async Task<ActionResult> DeleteBankAccount(Guid accountId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var success = await _revenueService.DeleteBankAccountAsync(userId, accountId);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank account for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("bank-accounts/{accountId}/set-primary")]
        public async Task<ActionResult> SetPrimaryBankAccount(Guid accountId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                var success = await _revenueService.SetPrimaryBankAccountAsync(userId, accountId);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary bank account for user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("request-payout")]
        public async Task<ActionResult> RequestPayout([FromBody] PayoutRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not found");
                }

                if (request.Amount <= 0)
                {
                    return BadRequest("Invalid amount");
                }

                var success = await _revenueService.RequestPayoutAsync(userId, request.Amount);
                if (!success)
                {
                    return BadRequest("Insufficient balance or other error");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting payout for user");
                return StatusCode(500, "Internal server error");
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        public class PayoutRequestDto
        {
            public decimal Amount { get; set; }
        }
    }
}

