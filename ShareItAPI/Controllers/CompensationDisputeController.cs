using BusinessObject.DTOs.IssueResolutionDto;
using Common.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CompensationDisputeServices;
using System;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class CompensationDisputeController : ControllerBase
    {
        private readonly ICompensationDisputeService _service;
        private readonly UserContextHelper _userContextHelper;

        public CompensationDisputeController(
            ICompensationDisputeService service,
            UserContextHelper userContextHelper)
        {
            _service = service;
            _userContextHelper = userContextHelper;
        }

        /// <summary>
        /// Lấy danh sách các tranh chấp đang chờ Admin xem xét
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingDisputes()
        {
            try
            {
                var disputes = await _service.GetPendingDisputesAsync();
                return Ok(disputes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching pending disputes", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chi tiết một vụ tranh chấp
        /// </summary>
        [HttpGet("{violationId}")]
        public async Task<IActionResult> GetDisputeDetail(Guid violationId)
        {
            try
            {
                var dispute = await _service.GetDisputeDetailAsync(violationId);
                if (dispute == null)
                {
                    return NotFound(new { message = "Dispute case not found" });
                }
                return Ok(dispute);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching dispute details", error = ex.Message });
            }
        }

        /// <summary>
        /// Admin tạo quyết định cuối cùng cho tranh chấp
        /// </summary>
        [HttpPost("resolve")]
        public async Task<IActionResult> CreateResolution([FromBody] CreateIssueResolutionDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var adminId = _userContextHelper.GetCurrentUserId();
                var resolution = await _service.CreateAdminResolutionAsync(dto, adminId);
                
                return Ok(new { message = "Resolution created successfully", data = resolution });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating resolution", error = ex.Message });
            }
        }

        /// <summary>
        /// Sync order status for all resolved violations
        /// Updates orders from "returned_with_issue" to "returned" if all violations are resolved
        /// </summary>
        [HttpPost("sync-order-status")]
        public async Task<IActionResult> SyncOrderStatus()
        {
            try
            {
                var updatedCount = await _service.SyncResolvedOrderStatusesAsync();
                return Ok(new { message = $"Successfully updated {updatedCount} order(s) to 'returned' status" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while syncing order status", error = ex.Message });
            }
        }
    }
}

