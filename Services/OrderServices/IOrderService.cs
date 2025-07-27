using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;

namespace Services.OrderServices
{
    public interface IOrderService
    {
        Task ChangeOrderStatus(Guid orderId, OrderStatus newStatus);
        Task CreateOrderAsync(CreateOrderDto dto);
        Task CancelOrderAsync(Guid orderId);
        Task UpdateOrderItemsAsync(Guid orderId, List<Guid> updatedItemIds, int rentalDays);
        Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
        Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<OrderFullDetailsDto>> GetAllAsync();
        Task<OrderWithDetailsDto> GetOrderDetailAsync(Guid orderId);
        Task MarkAsReceivedAsync(Guid orderId, bool paid);
        Task MarkAsReturnedAsync(Guid orderId);
        Task MarkAsApprovedAsync(Guid orderId);
        Task MarkAsShipingAsync(Guid orderId);
        Task CompleteTransactionAsync(Guid orderId);
        Task FailTransactionAsync(Guid orderId);
        Task<DashboardStatsDTO> GetDashboardStatsAsync();
        Task<DashboardStatsDTO> GetCustomerDashboardStatsAsync(Guid userId);
        Task<DashboardStatsDTO> GetProviderDashboardStatsAsync(Guid userId);
        Task<IEnumerable<OrderDto>> GetOrdersByProviderAsync(Guid providerId);
        Task<IEnumerable<OrderDto>> CreateOrderFromCartAsync(Guid customerId, CheckoutRequestDto checkoutRequestDto);
        Task MarkAsReturnedWithIssueAsync(Guid orderId);
        Task SendDamageReportEmailAsync(string toEmail, string subject, string body);

        //New Updated Methods for Order List Display
        Task<IEnumerable<OrderListDto>> GetProviderOrdersForListDisplayAsync(Guid providerId);
        Task<IEnumerable<OrderListDto>> GetCustomerOrdersForListDisplayAsync(Guid customerId);
        Task<IEnumerable<OrderListDto>> GetCustomerOrdersAsync(Guid customerId);
        Task<Order> GetOrderEntityByIdAsync(Guid orderId);
        Task<OrderDetailsDto> GetOrderDetailsAsync(Guid orderId);

        Task ClearCartItemsForOrderAsync(Order order);
        Task<Guid> RentAgainOrderAsync(Guid customerId, RentAgainRequestDto requestDto);
        Task<bool> UpdateOrderContactInfoAsync(Guid customerId, UpdateOrderContactInfoDto dto);
    }
}
