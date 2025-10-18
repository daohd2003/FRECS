using BusinessObject.Enums;
using BusinessObject.Models;

namespace Repositories.RevenueRepositories
{
    public interface IRevenueRepository
    {
        Task<List<Order>> GetOrdersInPeriodAsync(Guid providerId, DateTime start, DateTime end);
        Task<List<Order>> GetAllOrdersInPeriodAsync(Guid providerId, DateTime start, DateTime end);
        Task<List<Order>> GetOrdersByProviderIdAsync(Guid providerId);
        Task<decimal> GetTotalEarningsAsync(Guid providerId);
        Task<decimal> GetPendingAmountAsync(Guid providerId);
        Task<decimal> GetPenaltyRevenueInPeriodAsync(Guid providerId, DateTime start, DateTime end);
    }
}

