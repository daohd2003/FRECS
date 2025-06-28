using AutoMapper;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.CartRepositories;
using Repositories.EmailRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Repositories.UserRepositories;
using Services.NotificationServices;
using Microsoft.EntityFrameworkCore;

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
        private readonly ICartRepository _cartRepository;
        private readonly IUserRepository _userRepository;
        private readonly ShareItDbContext _context;
        private readonly IEmailRepository _emailRepository;
        public OrderService(
            IOrderRepository orderRepo,
            INotificationService notificationService,
            IHubContext<NotificationHub> hubContext,
            IRepository<Product> productRepository,
            IRepository<OrderItem> orderItemRepository,
            IMapper mapper,
            ICartRepository cartRepository,
            IUserRepository userRepository,
            ShareItDbContext context,
            IEmailRepository emailRepository)
        {
            _orderRepo = orderRepo;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _mapper = mapper;
            _productRepository = productRepository;
            _orderItemRepository = orderItemRepository;
            _cartRepository = cartRepository;
            _userRepository = userRepository;
            _context = context;
            _emailRepository = emailRepository;
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
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_transit, OrderStatus.in_use);

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

        public async Task MarkAsApprovedAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            order.Status = OrderStatus.approved;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.pending, OrderStatus.approved);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been marked as approved");
        }

        public async Task MarkAsShipingAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            order.Status = OrderStatus.in_transit;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.approved, OrderStatus.in_transit);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been marked as shipped");
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

        public async Task<DashboardStatsDTO> GetCustomerDashboardStatsAsync(Guid userId)
        {
            var orders = await _orderRepo.GetAllAsync();
            var userOrders = orders.Where(o => o.CustomerId == userId);

            var stats = new DashboardStatsDTO
            {
                PendingCount = userOrders.Count(o => o.Status == OrderStatus.pending),
                ApprovedCount = userOrders.Count(o => o.Status == OrderStatus.approved),
                InUseCount = userOrders.Count(o => o.Status == OrderStatus.in_use),
                ReturnedCount = userOrders.Count(o => o.Status == OrderStatus.returned),
                CancelledCount = userOrders.Count(o => o.Status == OrderStatus.cancelled)
            };

            return stats;
        }
        public async Task<DashboardStatsDTO> GetProviderDashboardStatsAsync(Guid userId)
        {
            var orders = await _orderRepo.GetAllAsync();
            var userOrders = orders.Where(o => o.ProviderId == userId);

            var stats = new DashboardStatsDTO
            {
                PendingCount = userOrders.Count(o => o.Status == OrderStatus.pending),
                ApprovedCount = userOrders.Count(o => o.Status == OrderStatus.approved),
                InUseCount = userOrders.Count(o => o.Status == OrderStatus.in_use),
                ReturnedCount = userOrders.Count(o => o.Status == OrderStatus.returned),
                CancelledCount = userOrders.Count(o => o.Status == OrderStatus.cancelled)
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

        public async Task<IEnumerable<OrderDto>> CreateOrderFromCartAsync(Guid customerId, CheckoutRequestDto checkoutRequestDto)
        {
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);

            if (cart == null || !cart.Items.Any())
            {
                throw new ArgumentException("Cart is empty or not found.");
            }

            // ... (Logic kiểm tra nhiều Provider, lấy ProviderId) ...
            var groupedCartItems = cart.Items
                .GroupBy(ci => ci.Product.ProviderId)
                .ToList();

            var createdOrders = new List<Order>();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var providerGroup in groupedCartItems)
                    {
                        var providerId = providerGroup.Key;
                        var provider = await _userRepository.GetByIdAsync(providerId);
                        if (provider == null)
                        {
                            throw new InvalidOperationException($"Provider with ID {providerId} not found.");
                        }

                        decimal totalAmount = 0;
                        var orderItems = new List<OrderItem>();

                        foreach (var cartItem in providerGroup)
                        {
                            var product = await _productRepository.GetByIdAsync(cartItem.ProductId);

                            if (product == null || product.AvailabilityStatus != BusinessObject.Enums.AvailabilityStatus.available || product.PricePerDay <= 0)
                            {
                                throw new InvalidOperationException($"Product '{product?.Name ?? cartItem.ProductId.ToString()}' is unavailable or has an invalid price.");
                            }

                            var orderItem = new OrderItem
                            {
                                Id = Guid.NewGuid(),
                                ProductId = cartItem.ProductId,
                                RentalDays = cartItem.RentalDays,
                                Quantity = cartItem.Quantity,
                                DailyRate = product.PricePerDay
                            };

                            orderItems.Add(orderItem);
                            totalAmount += orderItem.DailyRate * orderItem.RentalDays * orderItem.Quantity;
                        }

                        var newOrder = new Order
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customerId,
                            ProviderId = providerId,
                            Status = OrderStatus.pending,
                            TotalAmount = totalAmount,
                            RentalStart = checkoutRequestDto.RentalStart,
                            RentalEnd = checkoutRequestDto.RentalEnd,
                            Items = orderItems
                        };

                        await _orderRepo.AddAsync(newOrder);
                        createdOrders.Add(newOrder);

                        foreach (var cartItem in providerGroup)
                        {
                            await _cartRepository.DeleteCartItemAsync(cartItem);
                        }

                        // Gửi thông báo
                        await _notificationService.NotifyNewOrderCreated(newOrder.Id);
                        await _hubContext.Clients.Group($"notifications-{newOrder.CustomerId}")
                            .SendAsync("ReceiveNotification", $"New order #{newOrder.Id} created (Status: {newOrder.Status})");
                        await _hubContext.Clients.Group($"notifications-{newOrder.ProviderId}")
                            .SendAsync("ReceiveNotification", $"New order #{newOrder.Id} received (Status: {newOrder.Status})");
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Failed to create orders due to an internal error.", ex);
                }
            }

            // 5. Map Order entity sang OrderDto để trả về
            var orderDtos = createdOrders.Select(order =>
            {
                var dto = _mapper.Map<OrderDto>(order);
                dto.Items = order.Items.Select(i => _mapper.Map<OrderItemDto>(i)).ToList();
                return dto;
            }).ToList();

            return orderDtos;
        }
        public async Task MarkAsReturnedWithIssueAsync(Guid orderId)
        {
            // 1. Lấy order thật từ DB
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception("Order not found");

            // 2. Cập nhật trạng thái và thời gian
            order.Status = OrderStatus.returned_with_issue;
            order.UpdatedAt = DateTime.Now;

            // 3. Chỉ cập nhật 2 field: Status & UpdatedAt
            await _orderRepo.UpdateOnlyStatusAndTimeAsync(order);

            // 4. Gửi thông báo trạng thái
            await _notificationService.NotifyOrderStatusChange(
                order.Id,
                OrderStatus.in_use,
                OrderStatus.returned_with_issue
            );

            // 5. Gửi thông báo đến cả khách và người cho thuê
            await NotifyBothParties(
                order.CustomerId,
                order.ProviderId,
                $"Order #{order.Id} has been marked as returned with issue"
            );
            // 6. Gửi email đến khách hàng
            var customer = await _userRepository.GetByIdAsync(order.CustomerId);
            if (customer != null)
            {
                var subject = "Thông báo sản phẩm trả lại bị hư hỏng";
                var body = $@"
            <h3>Thông Báo Từ ShareIT Shop</h3>
            <p>Chúng tôi phát hiện đơn hàng mã <strong>{order.Id}</strong> có sản phẩm trả lại bị <strong>hư hỏng</strong>.</p>
            <p>Vui lòng phản hồi trong vòng <strong>3 ngày</strong> để tránh bị phạt.</p>
            <br />
            <p>Bạn có thể phản hồi tại mục <strong>'Reports liên quan đến bạn'</strong> trong hệ thống.</p>
            <p>Trân trọng,<br/>Đội ngũ hỗ trợ ShareIT</p>";

                await SendDamageReportEmailAsync(customer.Email, subject, body);
            }
        }
        public async Task SendDamageReportEmailAsync(string toEmail, string subject, string body)
        {
            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }

        public async Task<IEnumerable<OrderListDto>> GetProviderOrdersForListDisplayAsync(Guid providerId)
        {
            var orders = await _orderRepo.GetAll()
                                         .Where(o => o.ProviderId == providerId)
                                         .Include(o => o.Customer)
                                             .ThenInclude(c => c.Profile)
                                         .Include(o => o.Items)
                                             .ThenInclude(oi => oi.Product)
                                                 .ThenInclude(p => p.Images)
                                         .ToListAsync();

            return _mapper.Map<IEnumerable<OrderListDto>>(orders);
        }

        public async Task<IEnumerable<OrderListDto>> GetCustomerOrdersForListDisplayAsync(Guid customerId)
        {
            var orders = await _orderRepo.GetAll() 
                                         .Where(o => o.CustomerId == customerId)
                                         .Include(o => o.Customer)
                                             .ThenInclude(c => c.Profile)
                                         .Include(o => o.Items)
                                             .ThenInclude(oi => oi.Product)
                                                 .ThenInclude(p => p.Images)
                                         .ToListAsync();

            return _mapper.Map<IEnumerable<OrderListDto>>(orders);
        }
    }
}