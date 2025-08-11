using BusinessObject.Models;

namespace Services.PricingServices
{
    public interface IPricingService
    {
        Task<decimal> GetCurrentPriceAsync(Guid productId);
        Task<decimal> CalculateDiscountedPriceAsync(decimal originalPrice, int rentCount);
        Task<decimal> GetDiscountRateAsync();
        Task<int> GetMaxDiscountTimesAsync();
        Task UpdatePricingConfigAsync(string configKey, string configValue, Guid adminId);
        Task<string> GetPricingConfigValueAsync(string configKey);
    }
}
