using BusinessObject.DTOs.CustomerDashboard;
using BusinessObject.Models;

namespace Services.CustomerDashboardServices
{
    public interface ICustomerDashboardService
    {
        Task<CustomerSpendingStatsDto> GetSpendingStatsAsync(Guid customerId, string period);
        Task<List<SpendingTrendDto>> GetSpendingTrendAsync(Guid customerId, string period);
        Task<List<OrderStatusBreakdownDto>> GetOrderStatusBreakdownAsync(Guid customerId, string period);
        Task<List<SpendingByCategoryDto>> GetSpendingByCategoryAsync(Guid customerId, string period);
    }
}

