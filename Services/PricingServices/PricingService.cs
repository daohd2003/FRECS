using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.PricingServices
{
    public class PricingService : IPricingService
    {
        private readonly ShareItDbContext _context;

        // Default values
        private const decimal DEFAULT_DISCOUNT_RATE = 8.0m; // 8%
        private const int DEFAULT_MAX_DISCOUNT_TIMES = 5;

        public PricingService(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetCurrentPriceAsync(Guid productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                throw new ArgumentException("Product not found");

            return await CalculateDiscountedPriceAsync(product.PricePerDay, product.RentCount);
        }

        public async Task<decimal> CalculateDiscountedPriceAsync(decimal originalPrice, int rentCount)
        {
            var discountRate = await GetDiscountRateAsync();
            var maxDiscountTimes = await GetMaxDiscountTimesAsync();

            // Áp dụng discount tối đa theo số lần cấu hình
            var effectiveDiscountTimes = Math.Min(rentCount, maxDiscountTimes);
            
            // Tính tỷ lệ giảm giá tổng cộng
            var totalDiscountRate = effectiveDiscountTimes * (discountRate / 100);
            
            // Áp dụng giảm giá
            var discountedPrice = originalPrice * (1 - totalDiscountRate);
            
            // Đảm bảo giá không âm và làm tròn 2 chữ số thập phân
            return Math.Max(0, Math.Round(discountedPrice, 2));
        }

        public async Task<decimal> GetDiscountRateAsync()
        {
            var config = await GetPricingConfigValueAsync("RENTAL_DISCOUNT_RATE");
            if (decimal.TryParse(config, out var rate))
                return rate;
            return DEFAULT_DISCOUNT_RATE;
        }

        public async Task<int> GetMaxDiscountTimesAsync()
        {
            var config = await GetPricingConfigValueAsync("MAX_DISCOUNT_TIMES");
            if (int.TryParse(config, out var times))
                return times;
            return DEFAULT_MAX_DISCOUNT_TIMES;
        }

        public async Task<string> GetPricingConfigValueAsync(string configKey)
        {
            var config = await _context.PricingConfigs
                .Where(c => c.ConfigKey == configKey)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            return config?.ConfigValue ?? string.Empty;
        }

        public async Task UpdatePricingConfigAsync(string configKey, string configValue, Guid adminId)
        {
            // Kiểm tra xem config đã tồn tại chưa
            var existingConfig = await _context.PricingConfigs
                .Where(c => c.ConfigKey == configKey)
                .FirstOrDefaultAsync();

            if (existingConfig != null)
            {
                existingConfig.ConfigValue = configValue;
                existingConfig.UpdatedAt = DateTime.UtcNow;
                existingConfig.UpdatedByAdminId = adminId;
                _context.PricingConfigs.Update(existingConfig);
            }
            else
            {
                var newConfig = new PricingConfig
                {
                    Id = Guid.NewGuid(),
                    ConfigKey = configKey,
                    ConfigValue = configValue,
                    UpdatedByAdminId = adminId,
                    Description = GetConfigDescription(configKey)
                };
                _context.PricingConfigs.Add(newConfig);
            }

            await _context.SaveChangesAsync();
        }

        private string GetConfigDescription(string configKey)
        {
            return configKey switch
            {
                "RENTAL_DISCOUNT_RATE" => "Tỷ lệ giảm giá theo phần trăm cho mỗi lần thuê",
                "MAX_DISCOUNT_TIMES" => "Số lần thuê tối đa được áp dụng giảm giá",
                _ => "Cấu hình pricing"
            };
        }
    }
}
