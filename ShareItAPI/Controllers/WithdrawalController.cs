using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.WithdrawalDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.WithdrawalServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/withdrawals")]
    [Authorize]
    public class WithdrawalController : ControllerBase
    {
        private readonly IWithdrawalService _withdrawalService;

        public WithdrawalController(IWithdrawalService withdrawalService)
        {
            _withdrawalService = withdrawalService;
        }

        /// <summary>
        /// Provider requests a payout/withdrawal
        /// NOTE: Front-end should clear the form state when user navigates away or cancels the action
        /// to prevent previously entered amounts from remaining in the input field.
        /// </summary>
        [HttpPost("request")]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> RequestPayout([FromBody] WithdrawalRequestDto dto)
        {
            try
            {
                // Check for model validation errors (handles non-numeric and special character inputs)
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault();
                    
                    // Provide user-friendly message for invalid number format
                    if (errors == null || errors.Contains("valid") || errors.Contains("format"))
                    {
                        return BadRequest(new ApiResponse<string>("Please enter a valid number without special characters.", null));
                    }
                    
                    return BadRequest(new ApiResponse<string>(errors ?? "Invalid input.", null));
                }

                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await _withdrawalService.RequestPayoutAsync(providerId, dto);
                return Ok(new ApiResponse<WithdrawalResponseDto>("Withdrawal request submitted successfully", result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Provider gets their withdrawal history
        /// NOTE: Front-end should handle 401 Unauthorized responses by redirecting to the login page.
        /// </summary>
        [HttpGet("history")]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> GetWithdrawalHistory()
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var history = await _withdrawalService.GetWithdrawalHistoryAsync(providerId);
                return Ok(new ApiResponse<IEnumerable<WithdrawalHistoryDto>>("Success", history));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Provider gets their available balance for withdrawal
        /// </summary>
        [HttpGet("available-balance")]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> GetAvailableBalance()
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var balance = await _withdrawalService.GetAvailableBalanceAsync(providerId);
                return Ok(new ApiResponse<decimal>("Success", balance));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Get withdrawal request details by ID (Provider can only view their own, Admin can view all)
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetWithdrawalById(Guid id)
        {
            try
            {
                var withdrawal = await _withdrawalService.GetWithdrawalByIdAsync(id);
                if (withdrawal == null)
                    return NotFound(new ApiResponse<string>("Withdrawal request not found", null));

                return Ok(new ApiResponse<WithdrawalResponseDto>("Success", withdrawal));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Admin gets all pending withdrawal requests
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPendingRequests()
        {
            try
            {
                var requests = await _withdrawalService.GetPendingRequestsAsync();
                return Ok(new ApiResponse<IEnumerable<WithdrawalResponseDto>>("Success", requests));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Admin gets all withdrawal requests (pending, completed, rejected)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllRequests()
        {
            try
            {
                var requests = await _withdrawalService.GetAllRequestsAsync();
                return Ok(new ApiResponse<IEnumerable<WithdrawalResponseDto>>("Success", requests));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Admin processes (approve/reject) a withdrawal request
        /// </summary>
        [HttpPost("process")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ProcessWithdrawal([FromBody] ProcessWithdrawalRequestDto dto)
        {
            try
            {
                var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await _withdrawalService.ProcessWithdrawalAsync(adminId, dto);
                return Ok(new ApiResponse<WithdrawalResponseDto>("Withdrawal processed successfully", result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }
    }
}

