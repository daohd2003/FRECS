using BusinessObject.DTOs.SystemConfigDto;
using System;
using System.Threading.Tasks;

namespace Services.SystemConfigServices
{
    public interface ISystemConfigService
    {
        Task<CommissionRatesDto> GetCommissionRatesAsync();
        Task<bool> UpdateCommissionRatesAsync(decimal rentalRate, decimal purchaseRate, Guid adminId);
        Task<decimal> GetCommissionRateByTypeAsync(string transactionType);
    }
}
