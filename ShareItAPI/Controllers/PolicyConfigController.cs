using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PolicyConfigDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.PolicyConfigServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/policy-configs")]
    [ApiController]
    public class PolicyConfigController : ControllerBase
    {
        private readonly IPolicyConfigService _policyConfigService;

        public PolicyConfigController(IPolicyConfigService policyConfigService)
        {
            _policyConfigService = policyConfigService;
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
        /// Get all policies (Admin only)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllPolicies()
        {
            try
            {
                var response = await _policyConfigService.GetAllPoliciesAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }

        /// <summary>
        /// Get active policies (Public access for users to view)
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActivePolicies()
        {
            try
            {
                var response = await _policyConfigService.GetActivePoliciesAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }

        /// <summary>
        /// Get policy by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPolicyById(Guid id)
        {
            try
            {
                var response = await _policyConfigService.GetPolicyByIdAsync(id);
                if (response.Data == null)
                {
                    return NotFound(response);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }

        /// <summary>
        /// Create new policy (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreatePolicy([FromBody] CreatePolicyConfigDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>("Invalid data provided", null!));
                }

                var adminId = GetUserId();
                var response = await _policyConfigService.CreatePolicyAsync(dto, adminId);
                
                if (response.Data != null)
                {
                    return CreatedAtAction(nameof(GetPolicyById), new { id = response.Data.Id }, response);
                }
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }

        /// <summary>
        /// Update policy (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] UpdatePolicyConfigDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>("Invalid data provided", null!));
                }

                var adminId = GetUserId();
                var response = await _policyConfigService.UpdatePolicyAsync(id, dto, adminId);
                
                if (response.Data != null)
                {
                    return Ok(response);
                }
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }

        /// <summary>
        /// Delete policy (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeletePolicy(Guid id)
        {
            try
            {
                var response = await _policyConfigService.DeletePolicyAsync(id);
                
                if (response.Data)
                {
                    return Ok(response);
                }
                return NotFound(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null!));
            }
        }
    }
}
