using AutoMapper;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Services.NotificationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.OrderServices
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly INotificationService _notificationService;
        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<OrderItem> _orderItemRepository;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OrderService(
            IOrderRepository orderRepo,
            INotificationService notificationService,
            IHubContext<NotificationHub> hubContext,
            IRepository<Product> productRepository,
            IRepository<OrderItem> orderItemRepository,
            IMapper mapper)
        {
            _orderRepo = orderRepo;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _mapper = mapper;
            _productRepository = productRepository;
            _orderItemRepository = orderItemRepository;
        }

        public async Task CreateOrderAsync(CreateOrderDto dto)
        {
            // Dùng mapper để map DTO sang entity
            var order = _mapper.Map<Order>(dto);

            // Map Items → liên kết OrderId sau khi có Order Id
            foreach (var item in order.Items)
            {
                item.Id = Guid.NewGuid();
                item.OrderId = order.Id;
            }

            // Gọi repository để thêm vào DB
            await _orderRepo.AddAsync(order);

            await _notificationService.NotifyNewOrderCreated(order.Id);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification", $"New order #{order.Id} created (Status: {order.Status})");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification", $"New order #{order.Id} received (Status: {order.Status})");
        }

        public async Task ChangeOrderStatus(Guid orderId, OrderStatus newStatus)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            var oldStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.Now;

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(orderId, oldStatus, newStatus);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification",
                    $"Order #{orderId} status changed from {oldStatus} to {newStatus}");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification",
                    $"Order #{orderId} status changed from {oldStatus} to {newStatus}");
        }

        public async Task CancelOrderAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.Status = OrderStatus.cancelled;
            order.UpdatedAt = DateTime.Now;
            await _orderRepo.UpdateAsync(order);

            await _notificationService.NotifyOrderCancellation(orderId);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been cancelled");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been cancelled by customer");
        }

        public async Task UpdateOrderItemsAsync(Guid orderId, List<Guid> updatedProductIds, int rentalDays)
        {
            var order = await _orderRepo.GetByIdAsync(orderId); // Gồm cả OrderItems
            if (order == null) throw new Exception("Order not found");

            // Lấy danh sách hiện tại của OrderItems từ order.Items (EF tracking collection)
            var currentItems = order.Items.ToList();

            // Xóa các item không còn trong updatedProductIds
            var itemsToRemove = currentItems.Where(i => !updatedProductIds.Contains(i.ProductId)).ToList();
            foreach (var item in itemsToRemove)
            {
                await _orderItemRepository.DeleteAsync(item.Id);
            }

            // Cập nhật danh sách ProductId hiện tại sau khi xóa
            var currentProductIds = order.Items.Select(i => i.ProductId).ToList();

            // Thêm các sản phẩm mới có trong updatedProductIds nhưng chưa có trong order.Items
            var productIdsToAdd = updatedProductIds.Except(currentProductIds).ToList();

            foreach (var productId in productIdsToAdd)
            {
                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null) continue;

                var newItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId,
                    RentalDays = rentalDays,
                    DailyRate = product.PricePerDay
                };
                await _orderItemRepository.AddAsync(newItem);
            }
            
            // Gọi UpdateAsync để EF xử lý add/update/delete đúng cách
            await _orderRepo.UpdateAsync(order);

            await _notificationService.NotifyOrderItemsUpdate(orderId, updatedProductIds);

            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
              .SendAsync("ReceiveNotification",
                $"Order #{orderId} items updated. Current status: {order.Status}");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
              .SendAsync("ReceiveNotification",
                $"Order #{orderId} items updated. Current status: {order.Status}");
        }

        public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
        {
            var orderDtos = await _orderRepo.GetOrdersDetailAsync();

            return orderDtos;
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            var order = await _orderRepo.GetAllAsync();

            return order;
        }

        public async Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status)
        {
            return await _orderRepo.GetOrdersByStatusAsync(status);
        }

        public async Task<OrderWithDetailsDto> GetOrderDetailAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            return _mapper.Map<OrderWithDetailsDto>(order);
        }

        public async Task MarkAsReceivedAsync(Guid orderId, bool paid)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.Status = OrderStatus.in_use;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.approved, OrderStatus.in_use);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} marked as received (Paid: {paid})");
        }

        public async Task MarkAsReturnedAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.Status = OrderStatus.returned;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_use, OrderStatus.returned);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been marked as returned");
        }

        public async Task CompleteTransactionAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyTransactionCompleted(orderId, order.CustomerId);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Transaction for Order #{order.Id} has been completed");
        }

        public async Task FailTransactionAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyTransactionFailed(orderId, order.CustomerId);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Transaction for Order #{order.Id} has failed");
        }

        public async Task<DashboardStatsDTO> GetDashboardStatsAsync()
        {
            var orders = await _orderRepo.GetAllAsync();

            var stats = new DashboardStatsDTO
            {
                PendingCount = orders.Count(o => o.Status == OrderStatus.pending),
                ApprovedCount = orders.Count(o => o.Status == OrderStatus.approved),
                InUseCount = orders.Count(o => o.Status == OrderStatus.in_use),
                ReturnedCount = orders.Count(o => o.Status == OrderStatus.returned),
                CancelledCount = orders.Count(o => o.Status == OrderStatus.cancelled)
            };

            return stats;
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByProviderAsync(Guid providerId)
        {
            var orders = await _orderRepo.GetByProviderIdAsync(providerId);
            return _mapper.Map<IEnumerable<OrderDto>>(orders);
        }

        // Helper: Notify both customer and provider
        private async Task NotifyBothParties(Guid customerId, Guid providerId, string message)
        {
            await _hubContext.Clients.Group($"notifications-{customerId}").SendAsync("ReceiveNotification", message);
            await _hubContext.Clients.Group($"notifications-{providerId}").SendAsync("ReceiveNotification", message);
        }
    }
}