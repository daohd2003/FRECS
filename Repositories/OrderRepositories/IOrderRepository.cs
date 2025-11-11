using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.OrderRepositories
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetByProviderIdAsync(Guid providerId);
        Task<IEnumerable<OrderDto>> GetOrdersDetailAsync();
        Task UpdateOnlyStatusAndTimeAsync(Order order);
        Task<Order> GetOrderWithItemsAsync(Guid orderId);
        Task<bool> UpdateOrderContactInfoAsync(Order order);

        Task<string> GetOrderItemId(Guid customerId, Guid productId);
        
        Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync();
        Task<Order> GetOrderWithFullDetailsAsync(Guid orderId);
    }
}
