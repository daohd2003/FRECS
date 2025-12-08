using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DepositRefundServices;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [Route("api/depositrefunds")]
    [ApiController]
    [Authorize]
    public class DepositRefundController : ControllerBase
    {
        private readonly IDepositRefundService _refundService;

        public DepositRefundController(IDepositRefundService refundService)
        {
            _refundService = refundService;
        }

        /// <summary>
        /// Get all refund requests (Admin only)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllRefunds([FromQuery] string? status = null)
        {
            try
            {
                TransactionStatus? statusFilter = null;
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, true, out var parsedStatus))
                {
                    statusFilter = parsedStatus;
                }

                var refunds = await _refundService.GetAllRefundRequestsAsync(statusFilter);
                return Ok(new ApiResponse<IEnumerable<DepositRefundDto>>("Refund requests retrieved successfully", refunds));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<IEnumerable<DepositRefundDto>>($"Error retrieving refunds: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Get refund detail by ID
        /// </summary>
        [HttpGet("{refundId}")]
        public async Task<IActionResult> GetRefundDetail(Guid refundId)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<object>("Invalid user ID", null));
                }

                var refund = await _refundService.GetRefundDetailAsync(refundId);
                if (refund == null)
                {
                    return NotFound(new ApiResponse<object>("Refund request not found", null));
                }

                // Check authorization: Admin can view all, Customer can only view their own
                if (userRole != "admin" && refund.CustomerId != userId)
                {
                    return Forbid();
                }

                return Ok(new ApiResponse<object>("Refund detail retrieved successfully", refund));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error retrieving refund detail: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Feature: View deposit history
        /// The user views the history of funds deposited.
        /// Get customer's refund history (also available for providers who rent/buy from other providers)
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> GetMyRefunds()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var customerId))
                {
                    return Unauthorized(new ApiResponse<object>("Invalid user ID", null));
                }

                var refunds = await _refundService.GetCustomerRefundsAsync(customerId);
                return Ok(new ApiResponse<object>("Refund history retrieved successfully", refunds));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error retrieving refund history: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Process refund request (Approve or Reject)
        /// </summary>
        [HttpPost("process")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ProcessRefund([FromBody] ProcessRefundRequestDto request)
        {
            try
            {
                var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(adminIdClaim, out var adminId))
                {
                    return Unauthorized(new ApiResponse<object>("Invalid admin ID", null));
                }

                var success = await _refundService.ProcessRefundAsync(
                    request.RefundId,
                    adminId,
                    request.IsApproved,
                    request.BankAccountId,
                    request.Notes,
                    request.ExternalTransactionId
                );

                if (success)
                {
                    var action = request.IsApproved ? "approved" : "rejected";
                    return Ok(new ApiResponse<object>($"Refund request {action} successfully", null));
                }
                else
                {
                    return BadRequest(new ApiResponse<object>("Failed to process refund request", null));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error processing refund: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Reopen a rejected refund request (Admin can reopen any, Customer/Provider can only reopen their own)
        /// </summary>
        [HttpPost("reopen/{refundId}")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> ReopenRefund(Guid refundId)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<object>("Invalid user ID", null));
                }

                // If customer or provider, verify they own this refund request
                if (userRole == "customer" || userRole == "provider")
                {
                    var refund = await _refundService.GetRefundDetailAsync(refundId);
                    if (refund == null)
                    {
                        return NotFound(new ApiResponse<object>("Refund request not found", null));
                    }
                    if (refund.CustomerId != userId)
                    {
                        return Forbid();
                    }
                }

                var success = await _refundService.ReopenRefundAsync(refundId);
                if (success)
                {
                    return Ok(new ApiResponse<object>("Refund request reopened successfully", null));
                }
                else
                {
                    return BadRequest(new ApiResponse<object>("Failed to reopen refund request. Only rejected refunds can be reopened.", null));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error reopening refund: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Get pending refund count
        /// </summary>
        [HttpGet("pending/count")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPendingCount()
        {
            try
            {
                var count = await _refundService.GetPendingRefundCountAsync();
                return Ok(new ApiResponse<object>("Pending count retrieved successfully", new { count }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error retrieving count: {ex.Message}", null));
            }
        }
    }

    public class ProcessRefundRequestDto
    {
        public Guid RefundId { get; set; }
        public bool IsApproved { get; set; }
        public Guid? BankAccountId { get; set; } // BankAccount ID từ BankAccounts table
        public string? Notes { get; set; }
        public string? ExternalTransactionId { get; set; } // Mã giao dịch từ ngân hàng bên ngoài
    }
}

