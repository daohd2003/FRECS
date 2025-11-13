using BusinessObject.DTOs.SystemConfigDto;
using Repositories.SystemConfigRepositories;
using System;
using System.Threading.Tasks;

namespace Services.SystemConfigServices
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly ISystemConfigRepository _systemConfigRepository;

        public SystemConfigService(ISystemConfigRepository systemConfigRepository)
        {
            _systemConfigRepository = systemConfigRepository;
        }

        public async Task<CommissionRatesDto> GetCommissionRatesAsync()
        {
            var rentalConfig = await _systemConfigRepository.GetByKeyAsync("RENTAL_COMMISSION_RATE");
            var purchaseConfig = await _systemConfigRepository.GetByKeyAsync("PURCHASE_COMMISSION_RATE");

            var rentalRate = rentalConfig != null && decimal.TryParse(rentalConfig.Value, out decimal rental) 
                ? rental : 0.20m;
            var purchaseRate = purchaseConfig != null && decimal.TryParse(purchaseConfig.Value, out decimal purchase) 
                ? purchase : 0.10m;

            return new CommissionRatesDto
            {
                RentalCommissionRate = rentalRate * 100, // Convert to percentage
                PurchaseCommissionRate = purchaseRate * 100, // Convert to percentage
                LastUpdated = rentalConfig?.UpdatedAt ?? DateTime.UtcNow,
                UpdatedByAdminId = rentalConfig?.UpdatedByAdminId
            };
        }

        public async Task<bool> UpdateCommissionRatesAsync(decimal rentalRate, decimal purchaseRate, Guid adminId)
        {
            try
            {
                // Convert percentage to decimal (e.g., 20% -> 0.20)
                var rentalDecimal = rentalRate / 100m;
                var purchaseDecimal = purchaseRate / 100m;

                await _systemConfigRepository.UpdateConfigAsync(
                    "RENTAL_COMMISSION_RATE", 
                    rentalDecimal.ToString("0.00"), 
                    adminId
                );

                await _systemConfigRepository.UpdateConfigAsync(
                    "PURCHASE_COMMISSION_RATE", 
                    purchaseDecimal.ToString("0.00"), 
                    adminId
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<decimal> GetCommissionRateByTypeAsync(string transactionType)
        {
            return await _systemConfigRepository.GetCommissionRateAsync(transactionType);
        }
    }
}
