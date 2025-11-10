using BusinessObject.Models;

namespace Repositories.DepositRepositories
{
    public interface IDepositRepository
    {
        Task<List<Order>> GetCustomerOrdersWithDepositsAsync(Guid customerId);
    }
}

