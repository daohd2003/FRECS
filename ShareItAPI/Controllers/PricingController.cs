using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PricingDto;
using BusinessObject.Models;
using Common.Utilities;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.PricingServices;

namespace ShareItAPI.Controllers
{
    [Route("api/pricing")]
    [ApiController]
    public class PricingController : ControllerBase
    {
        private readonly IPricingService _pricingService;
        private readonly ShareItDbContext _context;
        private readonly UserContextHelper _userHelper;

        public PricingController(IPricingService pricingService, ShareItDbContext context, UserContextHelper userHelper)
        {
            _pricingService = pricingService;
            _context = context;
            _userHelper = userHelper;
        }

        /// <summary>
        /// Get current pricing configuration - Admin only
        /// </summary>
        [HttpGet("config")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPricingConfig()
        {
            var configs = await _context.PricingConfigs
                .Include(c => c.UpdatedByAdmin)
                .ThenInclude(u => u.Profile)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var configDtos = configs.Select(c => new PricingConfigDto
            {
                Id = c.Id,
                ConfigKey = c.ConfigKey,
                ConfigValue = c.ConfigValue,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                UpdatedByAdminName = c.UpdatedByAdmin?.Profile?.FullName ?? "Unknown"
            }).ToList();

            return Ok(new ApiResponse<object>("Pricing configuration fetched successfully.", configDtos));
        }

        /// <summary>
        /// Update pricing configuration - Admin only
        /// </summary>
        [HttpPost("config")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdatePricingConfig([FromBody] UpdatePricingConfigDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object>("Invalid data", ModelState));

            var adminId = _userHelper.GetCurrentUserId();
            
            // Validate config key first
            var validKeys = new[] { "RENTAL_DISCOUNT_RATE", "MAX_DISCOUNT_TIMES" };
            if (!validKeys.Contains(dto.ConfigKey))
            {
                return BadRequest(new ApiResponse<object>(
                    $"Invalid config key '{dto.ConfigKey}'. Valid keys are: {string.Join(", ", validKeys)}", 
                    null));
            }
            
            // Validate config values based on key
            if (dto.ConfigKey == "RENTAL_DISCOUNT_RATE")
            {
                if (!decimal.TryParse(dto.ConfigValue, out var rate) || rate < 0 || rate > 50)
                {
                    return BadRequest(new ApiResponse<object>(
                        "Discount rate must be a valid number between 0% and 50%", 
                        null));
                }
            }
            else if (dto.ConfigKey == "MAX_DISCOUNT_TIMES")
            {
                if (!int.TryParse(dto.ConfigValue, out var times) || times < 1 || times > 20)
                {
                    return BadRequest(new ApiResponse<object>(
                        "Max discount times must be a valid integer between 1 and 20", 
                        null));
                }
            }

            await _pricingService.UpdatePricingConfigAsync(dto.ConfigKey, dto.ConfigValue, adminId);

            return Ok(new ApiResponse<object>($"Configuration '{dto.ConfigKey}' updated successfully to '{dto.ConfigValue}'.", null));
        }

        /// <summary>
        /// Get product pricing details
        /// </summary>
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductPricing(Guid productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                return NotFound(new ApiResponse<object>("Product not found", null));

            var currentPrice = await _pricingService.GetCurrentPriceAsync(productId);
            var discountRate = await _pricingService.GetDiscountRateAsync();
            var maxDiscountTimes = await _pricingService.GetMaxDiscountTimesAsync();

            var effectiveDiscountTimes = Math.Min(product.RentCount, maxDiscountTimes);
            var totalDiscountPercentage = effectiveDiscountTimes * discountRate;

            var priceDto = new ProductPriceDto
            {
                ProductId = productId,
                ProductName = product.Name,
                OriginalPrice = product.PricePerDay,
                CurrentPrice = currentPrice,
                RentCount = product.RentCount,
                DiscountPercentage = totalDiscountPercentage,
                IsDiscounted = product.RentCount > 0 && effectiveDiscountTimes > 0
            };

            return Ok(new ApiResponse<object>("Product pricing fetched successfully.", priceDto));
        }

        /// <summary>
        /// Get current pricing settings summary
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetPricingSettings()
        {
            var discountRate = await _pricingService.GetDiscountRateAsync();
            var maxDiscountTimes = await _pricingService.GetMaxDiscountTimesAsync();

            var settings = new
            {
                DiscountRatePerRental = discountRate,
                MaxDiscountTimes = maxDiscountTimes,
                MaxTotalDiscount = discountRate * maxDiscountTimes,
                Description = $"Giảm {discountRate}% mỗi lần thuê, tối đa {maxDiscountTimes} lần (giảm tối đa {discountRate * maxDiscountTimes}%)"
            };

            return Ok(new ApiResponse<object>("Pricing settings fetched successfully.", settings));
        }

        /// <summary>
        /// Get available configuration keys - Admin only
        /// </summary>
        [HttpGet("config/keys")]
        [Authorize(Roles = "admin")]
        public IActionResult GetValidConfigKeys()
        {
            var validKeys = new[]
            {
                new { 
                    Key = "RENTAL_DISCOUNT_RATE", 
                    Description = "Tỷ lệ giảm giá theo phần trăm cho mỗi lần thuê (0-50)",
                    ValueType = "decimal",
                    Example = "8"
                },
                new { 
                    Key = "MAX_DISCOUNT_TIMES", 
                    Description = "Số lần thuê tối đa được áp dụng giảm giá (1-20)",
                    ValueType = "integer",
                    Example = "5"
                }
            };

            return Ok(new ApiResponse<object>("Valid configuration keys fetched successfully.", validKeys));
        }
    }
}
