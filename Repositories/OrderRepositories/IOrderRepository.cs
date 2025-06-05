using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.OrderRepositories
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetByProviderIdAsync(Guid providerId);
        Task<IEnumerable<OrderDto>> GetOrdersDetailAsync();

    }
}
