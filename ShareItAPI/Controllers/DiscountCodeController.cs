using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DiscountCodeServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountCodeController : ControllerBase
    {
        private readonly IDiscountCodeService _discountCodeService;

        public DiscountCodeController(IDiscountCodeService discountCodeService)
        {
            _discountCodeService = discountCodeService;
        }

        private Guid GetCustomerId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID claim not found.");
            }
            return Guid.Parse(userIdClaim);
        }

        /// <summary>
        /// Get all discount codes
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllDiscountCodes()
        {
            try
            {
                // Add logging
                Console.WriteLine("DiscountCodeController.GetAllDiscountCodes called");
                
                var discountCodes = await _discountCodeService.GetAllDiscountCodesAsync();
                Console.WriteLine($"Retrieved {discountCodes?.Count() ?? 0} discount codes");
                
                return Ok(new ApiResponse<IEnumerable<DiscountCodeDto>>("Discount codes retrieved successfully", discountCodes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllDiscountCodes: {ex}");
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving discount codes", ex.Message));
            }
        }

        /// <summary>
        /// Test endpoint to check if API is working
        /// </summary>
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new ApiResponse<string>("DiscountCode API is working", $"Current time: {DateTime.UtcNow}"));
        }

        /// <summary>
        /// Get discount code by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetDiscountCodeById(Guid id)
        {
            try
            {
                var discountCode = await _discountCodeService.GetDiscountCodeByIdAsync(id);
                if (discountCode == null)
                {
                    return NotFound(new ApiResponse<string>("Discount code not found", null));
                }

                return Ok(new ApiResponse<DiscountCodeDto>("Discount code retrieved successfully", discountCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving discount code", ex.Message));
            }
        }

        /// <summary>
        /// Get discount code by code
        /// </summary>
        [HttpGet("code/{code}")]
        public async Task<IActionResult> GetDiscountCodeByCode(string code)
        {
            try
            {
                var discountCode = await _discountCodeService.GetDiscountCodeByCodeAsync(code);
                if (discountCode == null)
                {
                    return NotFound(new ApiResponse<string>("Discount code not found", null));
                }

                // Check if code is still valid
                if (discountCode.Status != DiscountStatus.Active || discountCode.ExpirationDate <= DateTime.UtcNow)
                {
                    return BadRequest(new ApiResponse<string>("Discount code is not valid or has expired", null));
                }

                // Check if code is still available
                if (discountCode.UsedCount >= discountCode.Quantity)
                {
                    return BadRequest(new ApiResponse<string>("Discount code usage limit reached", null));
                }

                return Ok(new ApiResponse<DiscountCodeDto>("Discount code is valid", discountCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while validating discount code", ex.Message));
            }
        }

        /// <summary>
        /// Create new discount code
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateDiscountCode([FromBody] CreateDiscountCodeDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var discountCode = await _discountCodeService.CreateDiscountCodeAsync(createDto);
                return CreatedAtAction(nameof(GetDiscountCodeById), new { id = discountCode.Id }, 
                    new ApiResponse<DiscountCodeDto>("Discount code created successfully", discountCode));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while creating discount code", ex.Message));
            }
        }

        /// <summary>
        /// Update discount code
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDiscountCode(Guid id, [FromBody] UpdateDiscountCodeDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var updatedDiscountCode = await _discountCodeService.UpdateDiscountCodeAsync(id, updateDto);
                if (updatedDiscountCode == null)
                {
                    return NotFound(new ApiResponse<string>("Discount code not found", null));
                }

                return Ok(new ApiResponse<DiscountCodeDto>("Discount code updated successfully", updatedDiscountCode));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while updating discount code", ex.Message));
            }
        }

        /// <summary>
        /// Delete discount code
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteDiscountCode(Guid id)
        {
            try
            {
                var result = await _discountCodeService.DeleteDiscountCodeAsync(id);
                if (!result)
                {
                    return NotFound(new ApiResponse<string>("Discount code not found", null));
                }

                return Ok(new ApiResponse<string>("Discount code deleted successfully", null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while deleting discount code", ex.Message));
            }
        }

        /// <summary>
        /// Get active discount codes
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveDiscountCodes()
        {
            try
            {
                var discountCodes = await _discountCodeService.GetActiveDiscountCodesAsync();
                return Ok(new ApiResponse<IEnumerable<DiscountCodeDto>>("Active discount codes retrieved successfully", discountCodes));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving active discount codes", ex.Message));
            }
        }

        /// <summary>
        /// Get usage history for a discount code
        /// </summary>
        [HttpGet("{id}/usage-history")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUsageHistory(Guid id)
        {
            try
            {
                var usageHistory = await _discountCodeService.GetUsageHistoryAsync(id);
                return Ok(new ApiResponse<IEnumerable<UsedDiscountCodeDto>>("Usage history retrieved successfully", usageHistory));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving usage history", ex.Message));
            }
        }

        /// <summary>
        /// Check if discount code is unique
        /// </summary>
        [HttpGet("check-unique/{code}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CheckCodeUnique(string code, [FromQuery] Guid? excludeId = null)
        {
            try
            {
                var isUnique = await _discountCodeService.IsCodeUniqueAsync(code, excludeId);
                return Ok(new ApiResponse<bool>(isUnique ? "Code is available" : "Code already exists", isUnique));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while checking code uniqueness", ex.Message));
            }
        }

        /// <summary>
        /// Get all available discount codes for the current user
        /// </summary>
        [HttpGet("user-available")]
        [Authorize]
        public async Task<IActionResult> GetUserAvailableDiscountCodes()
        {
            try
            {
                var userId = GetCustomerId();
                var discountCodes = await _discountCodeService.GetActiveDiscountCodesAsync();
                
                // Get discount codes already used by this user
                var usedDiscountCodeIds = await _discountCodeService.GetUsedDiscountCodeIdsByUserAsync(userId);
                
                // Filter only active codes that haven't expired, still have quantity, and NOT used by this user
                var availableCodes = discountCodes.Where(dc => 
                    dc.Status == DiscountStatus.Active && 
                    dc.ExpirationDate > DateTime.UtcNow && 
                    dc.UsedCount < dc.Quantity &&
                    !usedDiscountCodeIds.Contains(dc.Id)
                ).ToList();

                return Ok(new ApiResponse<IEnumerable<DiscountCodeDto>>("Available discount codes retrieved successfully", availableCodes));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving available discount codes", ex.Message));
            }
        }
    }
}
