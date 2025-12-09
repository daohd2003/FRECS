using AutoMapper;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.CartRepositories;
using Repositories.EmailRepositories;
using Repositories.NotificationRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Repositories.UserRepositories;
using Repositories.SystemConfigRepositories;
using Services.NotificationServices;
using Services.DiscountCalculationServices;
using BusinessObject.Utilities;
using Microsoft.EntityFrameworkCore;
using Repositories.ProductRepositories;
using CloudinaryDotNet.Actions;
using BusinessObject.DTOs.ApiResponses;



namespace Services.OrderServices
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IProductRepository _productRepo;
        private readonly INotificationService _notificationService;
        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<OrderItem> _orderItemRepository;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ICartRepository _cartRepository;
        private readonly IUserRepository _userRepository;
        private readonly ShareItDbContext _context;
        private readonly IEmailRepository _emailRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ISystemConfigRepository _systemConfigRepository;
        private readonly IDiscountCalculationService _discountCalculationService;

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
            IEmailRepository emailRepository,
            IProductRepository productRepo,
            INotificationRepository notificationRepository,
            ISystemConfigRepository systemConfigRepository,
            IDiscountCalculationService discountCalculationService
            )
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
            _productRepo = productRepo;
            _notificationRepository = notificationRepository;
            _systemConfigRepository = systemConfigRepository;
            _discountCalculationService = discountCalculationService;
        }

        /// <summary>
        /// Tạo đơn hàng mới từ giỏ hàng hoặc từ request trực tiếp
        /// Xử lý: tính toán giá, phí hoa hồng, tiền cọc, gửi thông báo cho customer và provider
        /// </summary>
        /// <param name="dto">Thông tin đơn hàng cần tạo</param>
        public async Task CreateOrderAsync(CreateOrderDto dto)
        {
            // Bước 1: Chuyển đổi DTO thành entity Order bằng AutoMapper
            var order = _mapper.Map<Order>(dto);

            // Bước 2: Lấy tỷ lệ hoa hồng từ cấu hình hệ thống
            // Hoa hồng cho thuê và mua có thể khác nhau
            var rentalCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("RENTAL_COMMISSION_RATE");
            var purchaseCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("PURCHASE_COMMISSION_RATE");

            // Bước 3: Xử lý từng OrderItem trong đơn hàng
            foreach (var item in order.Items)
            {
                item.Id = Guid.NewGuid(); // Tạo ID mới cho item
                item.OrderId = order.Id; // Liên kết item với order
                
                // Tính doanh thu của item này (để tính hoa hồng)
                // - Nếu mua: giá * số lượng
                // - Nếu thuê: giá/ngày * số ngày * số lượng
                decimal itemRevenue = item.TransactionType == BusinessObject.Enums.TransactionType.purchase
                    ? item.DailyRate * item.Quantity
                    : item.DailyRate * (item.RentalDays ?? 1) * item.Quantity;
                
                // Tính và lưu hoa hồng cho từng item
                if (item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                {
                    item.RentalCommissionRate = rentalCommissionRate;
                    item.CommissionAmount = itemRevenue * rentalCommissionRate; // Hoa hồng = doanh thu * tỷ lệ
                }
                else // purchase
                {
                    item.PurchaseCommissionRate = purchaseCommissionRate;
                    item.CommissionAmount = itemRevenue * purchaseCommissionRate;
                }
            }

            // Bước 4: Tính tổng tiền hàng (subtotal) - không bao gồm tiền cọc
            if (order.Subtotal == 0 && order.Items.Any())
            {
                order.Subtotal = order.Items.Sum(item => 
                    item.TransactionType == BusinessObject.Enums.TransactionType.purchase
                        ? item.DailyRate * item.Quantity // Mua: giá * số lượng
                        : item.DailyRate * (item.RentalDays ?? 1) * item.Quantity); // Thuê: giá/ngày * số ngày * số lượng
            }
            
            // Bước 5: Tính tổng tiền cọc (chỉ áp dụng cho đơn thuê)
            if (order.TotalDeposit == 0 && order.Items.Any())
            {
                order.TotalDeposit = order.Items
                    .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental) // Chỉ lấy item thuê
                    .Sum(item => item.DepositPerUnit * item.Quantity); // Cọc/sản phẩm * số lượng
            }
            
            // Bước 6: Tính tổng tiền phải trả = tiền hàng + tiền cọc
            if (order.TotalAmount == 0)
            {
                order.TotalAmount = order.Subtotal + order.TotalDeposit;
            }

            // Bước 7: Lưu đơn hàng vào database
            await _orderRepo.AddAsync(order);

            // Bước 8: Gửi thông báo qua email và trong hệ thống
            await _notificationService.NotifyNewOrderCreated(order.Id);

            // Bước 9: Gửi thông báo real-time qua SignalR
            // Thông báo cho customer (người mua/thuê)
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification", $"New order #{order.Id} created (Status: {order.Status})");

            // Thông báo cho provider (người bán/cho thuê)
            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification", $"New order #{order.Id} received (Status: {order.Status})");
        }

        /// <summary>
        /// Thay đổi trạng thái đơn hàng (pending → approved → shipping → delivered, etc.)
        /// Cập nhật số lượng sản phẩm và gửi thông báo cho các bên liên quan
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần thay đổi trạng thái</param>
        /// <param name="newStatus">Trạng thái mới</param>
        public async Task ChangeOrderStatus(Guid orderId, OrderStatus newStatus)
        {
            // Bước 1: Tìm đơn hàng theo ID
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            // Bước 2: Lưu trạng thái cũ để so sánh và gửi thông báo
            var oldStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime(); // Cập nhật thời gian sửa đổi

            // Bước 3: Cập nhật số lượng sản phẩm dựa trên thay đổi trạng thái
            // Ví dụ: khi approved → giảm stock, khi cancelled → hoàn stock
            await UpdateProductCounts(order, oldStatus, newStatus);

            // Bước 4: Lưu thay đổi vào database
            await _orderRepo.UpdateAsync(order);
            
            // Bước 5: Gửi thông báo qua email và trong hệ thống
            await _notificationService.NotifyOrderStatusChange(orderId, oldStatus, newStatus);

            // Bước 6: Gửi thông báo real-time qua SignalR cho customer
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification",
                    $"Order #{orderId} status changed from {FormatOrderStatusText(oldStatus)} to {FormatOrderStatusText(newStatus)}");

            // Gửi thông báo real-time cho provider
            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification",
                    $"Order #{orderId} status changed from {FormatOrderStatusText(oldStatus)} to {FormatOrderStatusText(newStatus)}");
        }

        /// <summary>
        /// Hủy đơn hàng và hoàn lại số lượng sản phẩm vào kho (nếu đã thanh toán)
        /// Gửi thông báo hủy đơn cho customer và provider
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần hủy</param>
        public async Task CancelOrderAsync(Guid orderId)
        {
            // Bước 1: Tìm đơn hàng theo ID
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            var oldStatus = order.Status;

            // Bước 2: Nếu đơn hàng đã được thanh toán (approved/in_transit/in_use)
            // thì cần hoàn lại số lượng sản phẩm vào kho
            if (oldStatus == OrderStatus.approved || oldStatus == OrderStatus.in_transit || oldStatus == OrderStatus.in_use)
            {
                foreach (var item in order.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        // Hoàn lại số lượng tùy theo loại giao dịch
                        if (item.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                        {
                            product.PurchaseQuantity += item.Quantity; // Hoàn số lượng mua
                        }
                        else // Rental
                        {
                            product.RentalQuantity += item.Quantity; // Hoàn số lượng thuê
                        }
                        await _productRepository.UpdateAsync(product);
                    }
                }
            }

            // Bước 3: Cập nhật trạng thái đơn hàng thành cancelled
            order.Status = OrderStatus.cancelled;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();
            await _orderRepo.UpdateAsync(order);

            // Bước 4: Gửi thông báo hủy đơn
            await _notificationService.NotifyOrderCancellation(orderId);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been cancelled");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been cancelled by customer");
        }

        public async Task DeleteOrderAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            // Only allow deleting cancelled orders
            if (order.Status != OrderStatus.cancelled)
            {
                throw new InvalidOperationException("Only cancelled orders can be deleted");
            }

            // Delete related records in correct order to avoid foreign key constraints
            
            // 1. Delete all notifications related to this order
            await _notificationRepository.DeleteByOrderIdAsync(orderId);

            // 2. Delete all order items related to this order
            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .ToListAsync();
            
            if (orderItems.Any())
            {
                _context.OrderItems.RemoveRange(orderItems);
                await _context.SaveChangesAsync();
            }

            // 3. Finally delete the order itself
            // Note: Transactions don't have OrderId FK, they have one-to-many relationship with Orders
            await _orderRepo.DeleteAsync(orderId);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{order.CustomerId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been permanently deleted");

            await _hubContext.Clients.Group($"notifications-{order.ProviderId}")
                .SendAsync("ReceiveNotification", $"Order #{orderId} has been permanently deleted");
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

        public async Task<IEnumerable<OrderFullDetailsDto>> GetAllAsync()
        {
            var order = await _orderRepo.GetAllAsync();

            return _mapper.Map<IEnumerable<OrderFullDetailsDto>>(order);
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
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

            // Update counts if needed (no action for in_use status)
            await UpdateProductCounts(order, OrderStatus.in_transit, OrderStatus.in_use);

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_transit, OrderStatus.in_use);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} marked as received (Paid: {paid})");
        }

        public async Task MarkAsReturnedAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            // Check if order was already marked as returned to prevent duplicate rent count increments
            if (order.Status == OrderStatus.returned)
            {
                throw new InvalidOperationException("Order is already marked as returned");
            }

            // Validate state transition: Can only mark as returned from 'returning' status
            if (order.Status != OrderStatus.returning)
            {
                throw new InvalidOperationException($"Cannot mark order as returned. Order must be in 'returning' status. Current status: {order.Status}");
            }

            order.Status = OrderStatus.returned;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

            // Increment rent count for rental products when returned
            await UpdateProductCounts(order, OrderStatus.in_use, OrderStatus.returned);
            
            // Auto-create deposit refund request if order has deposit
            await CreateDepositRefundIfNeeded(order);

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_use, OrderStatus.returned);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been marked as returned");
        }

        /// <summary>
        /// Automatically creates a deposit refund request when order is returned
        /// Calculates refund amount = Original Deposit - Total Penalties
        /// </summary>
        private async Task CreateDepositRefundIfNeeded(Order order)
        {
            // Only create refund if order has deposit amount
            if (order.TotalDeposit <= 0)
            {
                return;
            }

            // Check if deposit refund already exists for this order (avoid duplicate)
            var existingRefund = await _context.DepositRefunds
                .FirstOrDefaultAsync(dr => dr.OrderId == order.Id);

            if (existingRefund != null)
            {
                // Refund already exists, skip creation
                return;
            }

            // Calculate total penalties from rental violations
            var totalPenalties = await _context.RentalViolations
                .Where(v => order.Items.Select(i => i.Id).Contains(v.OrderItemId))
                .SumAsync(v => v.PenaltyAmount);

            // Calculate refund amount (cannot be negative)
            var refundAmount = Math.Max(0, order.TotalDeposit - totalPenalties);

            // Create deposit refund record
            var depositRefund = new BusinessObject.Models.DepositRefund
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                OriginalDepositAmount = order.TotalDeposit,
                TotalPenaltyAmount = totalPenalties,
                RefundAmount = refundAmount,
                Status = BusinessObject.Enums.TransactionStatus.initiated,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                Notes = totalPenalties > 0 
                    ? $"Deposit refund with penalty deduction. Total penalties: {totalPenalties:N0} ₫" 
                    : "Full deposit refund - no violations"
            };

            await _context.DepositRefunds.AddAsync(depositRefund);
            await _context.SaveChangesAsync();

            // Notify customer about refund request
            await _notificationService.SendNotification(
                order.CustomerId,
                $"Your deposit refund request has been created. Refund amount: {refundAmount:N0} ₫",
                BusinessObject.Enums.NotificationType.order,
                order.Id
            );
        }

        public async Task MarkAsApprovedAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            
            order.Status = OrderStatus.approved;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();
            
            // Update buy count for purchase orders when approved
            await UpdateProductCounts(order, OrderStatus.pending, OrderStatus.approved);
            
            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.pending, OrderStatus.approved);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been paid");
        }

        public async Task MarkAsShipingAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            order.Status = OrderStatus.in_transit;
            order.DeliveredDate = DateTimeHelper.GetVietnamTime();
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();
            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.approved, OrderStatus.in_transit);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been marked as shipped");
        }

        public async Task ConfirmDeliveryAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            
            // Chỉ cho phép xác nhận khi đơn hàng đang ở trạng thái in_transit
            if (order.Status != OrderStatus.in_transit)
                throw new Exception("Order must be in transit status to confirm delivery");
            
            order.Status = OrderStatus.in_use;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();
            await _orderRepo.UpdateAsync(order);
            
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_transit, OrderStatus.in_use);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} has been confirmed as received and is now in use");
        }

        public async Task MarkAsReturningAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            
            // Chỉ cho phép mark as returning khi đơn hàng đang ở trạng thái in_use
            if (order.Status != OrderStatus.in_use)
                throw new Exception("Order must be in use status to mark as returning");
            
            order.Status = OrderStatus.returning;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();
            await _orderRepo.UpdateAsync(order);
            
            await _notificationService.NotifyOrderStatusChange(order.Id, OrderStatus.in_use, OrderStatus.returning);
            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Order #{order.Id} is being returned by customer");
        }

        public async Task CompleteTransactionAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

            await _orderRepo.UpdateAsync(order);
            await _notificationService.NotifyTransactionCompleted(orderId, order.CustomerId);

            await NotifyBothParties(order.CustomerId, order.ProviderId, $"Transaction for Order #{order.Id} has been completed");
        }

        public async Task FailTransactionAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

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

                        // Get commission rates from database
                        var rentalCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("RENTAL_COMMISSION_RATE");
                        var purchaseCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("PURCHASE_COMMISSION_RATE");

                        decimal subtotalAmount = 0;
                        decimal depositAmount = 0;
                        var orderItems = new List<OrderItem>();

                        DateTime? minRentalStart = null;
                        DateTime? maxRentalEnd = null;

                        foreach (var cartItem in providerGroup)
                        {
                            var product = await _productRepository.GetByIdAsync(cartItem.ProductId);

                            if (product == null || product.AvailabilityStatus != BusinessObject.Enums.AvailabilityStatus.available)
                            {
                                throw new InvalidOperationException($"Product '{product?.Name ?? cartItem.ProductId.ToString()}' is unavailable.");
                            }

                            // Validate availability based on transaction type
                            if (cartItem.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                            {
                                // Validate purchase availability
                                if (product.PurchaseStatus != BusinessObject.Enums.PurchaseStatus.Available || 
                                    product.PurchasePrice <= 0)
                                {
                                    throw new InvalidOperationException($"Product '{product.Name}' is not available for purchase.");
                                }
                                
                                // Validate stock quantity for purchase
                                if (cartItem.Quantity > product.PurchaseQuantity)
                                {
                                    throw new InvalidOperationException($"Quantity exceeds available stock. Only {product.PurchaseQuantity} units available for purchase of '{product.Name}'.");
                                }
                            }
                            else // Rental
                            {
                                // Validate rental availability
                                if (product.RentalStatus != BusinessObject.Enums.RentalStatus.Available || 
                                    product.PricePerDay <= 0)
                                {
                                    throw new InvalidOperationException($"Product '{product.Name}' is not available for rental.");
                                }
                                
                                // Validate stock quantity for rental
                                if (cartItem.Quantity > product.RentalQuantity)
                                {
                                    throw new InvalidOperationException($"Quantity exceeds available stock. Only {product.RentalQuantity} units available for rental of '{product.Name}'.");
                                }
                            }

                            var orderItem = new OrderItem
                            {
                                Id = Guid.NewGuid(),
                                ProductId = cartItem.ProductId,
                                TransactionType = cartItem.TransactionType, // Get from cartItem
                                RentalDays = cartItem.RentalDays,
                                Quantity = cartItem.Quantity,
                                // Set price based on transaction type
                                DailyRate = cartItem.TransactionType == BusinessObject.Enums.TransactionType.purchase 
                                    ? product.PurchasePrice 
                                    : product.PricePerDay,
                                // Set deposit per unit for rental items
                                DepositPerUnit = cartItem.TransactionType == BusinessObject.Enums.TransactionType.rental 
                                    ? product.SecurityDeposit 
                                    : 0m
                            };

                            // Calculate and store commission for each item
                            decimal itemRevenue = cartItem.TransactionType == BusinessObject.Enums.TransactionType.purchase
                                ? orderItem.DailyRate * orderItem.Quantity
                                : orderItem.DailyRate * (orderItem.RentalDays ?? 1) * orderItem.Quantity;
                            
                            if (cartItem.TransactionType == BusinessObject.Enums.TransactionType.rental)
                            {
                                orderItem.RentalCommissionRate = rentalCommissionRate;
                                orderItem.CommissionAmount = itemRevenue * rentalCommissionRate;
                            }
                            else // purchase
                            {
                                orderItem.PurchaseCommissionRate = purchaseCommissionRate;
                                orderItem.CommissionAmount = itemRevenue * purchaseCommissionRate;
                            }

                            orderItems.Add(orderItem);
                            
                            // Calculate subtotal (chỉ giá thuê/mua, không bao gồm cọc)
                            if (cartItem.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                            {
                                subtotalAmount += orderItem.DailyRate * orderItem.Quantity;
                            }
                            else // Rental
                            {
                                subtotalAmount += orderItem.DailyRate * (orderItem.RentalDays ?? 1) * orderItem.Quantity;
                                // Calculate deposit separately
                                depositAmount += orderItem.DepositPerUnit * orderItem.Quantity;
                            }
                        }
                        var rentalStart = providerGroup.Min(ci => ci.StartDate);
                        var rentalEnd = providerGroup.Max(ci => ci.EndDate);

                        // Apply discount code if provided
                        decimal discountAmount = 0;
                        Guid? discountCodeIdToStore = null;
                        
                        if (checkoutRequestDto.DiscountCodeId.HasValue)
                        {
                            var discountCode = await _context.DiscountCodes.FindAsync(checkoutRequestDto.DiscountCodeId.Value);
                            
                            if (discountCode != null && 
                                discountCode.Status == BusinessObject.Enums.DiscountStatus.Active &&
                                discountCode.ExpirationDate > DateTimeHelper.GetVietnamTime() &&
                                discountCode.UsedCount < discountCode.Quantity)
                            {
                                // Determine applicable subtotal based on discount code UsageType
                                decimal applicableSubtotal = 0;
                                
                                if (discountCode.UsageType == BusinessObject.Enums.DiscountUsageType.Rental)
                                {
                                    // Only apply discount to rental items
                                    applicableSubtotal = orderItems
                                        .Where(oi => oi.TransactionType == BusinessObject.Enums.TransactionType.rental)
                                        .Sum(oi => oi.DailyRate * (oi.RentalDays ?? 0) * oi.Quantity);
                                }
                                else if (discountCode.UsageType == BusinessObject.Enums.DiscountUsageType.Purchase)
                                {
                                    // Only apply discount to purchase items
                                    applicableSubtotal = orderItems
                                        .Where(oi => oi.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                                        .Sum(oi => oi.DailyRate * oi.Quantity);
                                }
                                
                                // Calculate discount amount
                                if (discountCode.DiscountType == BusinessObject.Enums.DiscountType.Percentage)
                                {
                                    discountAmount = Math.Round(applicableSubtotal * (discountCode.Value / 100), 2);
                                }
                                else // Fixed amount
                                {
                                    discountAmount = discountCode.Value;
                                }
                                
                                // Ensure discount doesn't exceed applicable subtotal
                                discountAmount = Math.Min(discountAmount, applicableSubtotal);
                                
                                discountCodeIdToStore = discountCode.Id;
                            }
                        }

                        // Calculate automatic discounts for rental items
                        decimal itemRentalCountDiscount = 0;
                        decimal loyaltyDiscount = 0;
                        decimal itemRentalCountDiscountPercent = 0;
                        decimal loyaltyDiscountPercent = 0;
                        
                        var rentalItems = orderItems.Where(oi => oi.TransactionType == BusinessObject.Enums.TransactionType.rental).ToList();
                        if (rentalItems.Any())
                        {
                            // Get product IDs for item rental count discount calculation
                            var productIds = rentalItems.Select(oi => oi.ProductId).ToList();
                            
                            // Calculate base daily total (giá/ngày × số lượng) for Item Rental Discount
                            // This is FIXED regardless of rental days - only increases with quantity
                            var baseDailyTotal = rentalItems.Sum(oi => oi.DailyRate * oi.Quantity);
                            
                            // Calculate base amount for Loyalty Discount (fixed, use average daily rate as base)
                            // This is FIXED - does not change with quantity or days
                            var baseAmountForLoyalty = rentalItems.Average(oi => oi.DailyRate);

                            // Calculate auto discounts
                            // - Item Rental Discount: applied to baseDailyTotal (fixed, increases with quantity only)
                            // - Loyalty Discount: applied to baseAmountForLoyalty (fixed, does not change with quantity or days)
                            var autoDiscount = await _discountCalculationService.CalculateAutoDiscountsWithItemsAsync(
                                customerId, 
                                productIds, 
                                baseDailyTotal,
                                baseAmountForLoyalty);

                            itemRentalCountDiscount = autoDiscount.ItemRentalCountDiscountAmount;
                            loyaltyDiscount = autoDiscount.LoyaltyDiscountAmount;
                            itemRentalCountDiscountPercent = autoDiscount.ItemRentalCountDiscountPercent;
                            loyaltyDiscountPercent = autoDiscount.LoyaltyDiscountPercent;
                        }

                        // Total discount = discount code + auto discounts
                        var totalDiscount = discountAmount + itemRentalCountDiscount + loyaltyDiscount;

                        var newOrder = new Order
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customerId,
                            ProviderId = providerId,
                            Status = OrderStatus.pending,
                            Subtotal = subtotalAmount, // Chỉ giá thuê/mua
                            TotalDeposit = depositAmount, // Chỉ tiền cọc
                            DiscountCodeId = discountCodeIdToStore,
                            DiscountAmount = discountAmount, // Chỉ discount từ code
                            ItemRentalCountDiscount = itemRentalCountDiscount,
                            LoyaltyDiscount = loyaltyDiscount,
                            ItemRentalCountDiscountPercent = itemRentalCountDiscountPercent,
                            LoyaltyDiscountPercent = loyaltyDiscountPercent,
                            TotalAmount = subtotalAmount + depositAmount - totalDiscount, // Tổng = subtotal + deposit - all discounts
                            RentalStart = rentalStart,
                            RentalEnd = rentalEnd,
                            Items = orderItems,
                            CustomerFullName = checkoutRequestDto.CustomerFullName,
                            CreatedAt = DateTimeHelper.GetVietnamTime(),
                            CustomerEmail = checkoutRequestDto.CustomerEmail,
                            CustomerPhoneNumber = checkoutRequestDto.CustomerPhoneNumber,
                            DeliveryAddress = checkoutRequestDto.DeliveryAddress,
                            HasAgreedToPolicies = checkoutRequestDto.HasAgreedToPolicies
                        };

                        await _orderRepo.AddAsync(newOrder);
                        createdOrders.Add(newOrder);

                        // NOTE: Do NOT deduct stock quantities here (order is still pending/unpaid)
                        // Quantity will be deducted when order status changes to 'approved' (payment confirmed)
                        // This prevents stock from being locked for unpaid/abandoned orders

                        //foreach (var cartItem in providerGroup)
                        //{
                        //    await _cartRepository.DeleteCartItemAsync(cartItem);
                        //}

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

            // Validate state transition: Can only mark as returned_with_issue from 'returning' status
            if (order.Status != OrderStatus.returning)
            {
                throw new InvalidOperationException($"Cannot mark order as returned with issue. Order must be in 'returning' status. Current status: {order.Status}");
            }

            // 2. Cập nhật trạng thái và thời gian
            order.Status = OrderStatus.returned_with_issue;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

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
            <h3>Thông Báo Từ FRECS Shop</h3>
            <p>Chúng tôi phát hiện đơn hàng mã <strong>{order.Id}</strong> có sản phẩm trả lại bị <strong>hư hỏng</strong>.</p>
            <p>Vui lòng phản hồi trong vòng <strong>3 ngày</strong> để tránh bị phạt.</p>
            <br />
            <p>Bạn có thể phản hồi tại mục <strong>'Reports liên quan đến bạn'</strong> trong hệ thống.</p>
            <p>Trân trọng,<br/>Đội ngũ hỗ trợ FRECS</p>";

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
                                         .OrderByDescending(o => o.CreatedAt)
                                         .ToListAsync();

            return _mapper.Map<IEnumerable<OrderListDto>>(orders);
        }

        public async Task<IEnumerable<OrderListDto>> GetCustomerOrdersAsync(Guid customerId)
        {
            // 1. Lấy tất cả đơn hàng của khách hàng, sắp xếp theo thời gian tạo TĂNG DẦN
            var allCustomerOrders = await _context.Orders
                                            .Where(o => o.CustomerId == customerId)
                                            .Include(o => o.Customer.Profile)
                                            .Include(o => o.Items)
                                                .ThenInclude(oi => oi.Product)
                                                    .ThenInclude(p => p.Images)
                                            .OrderBy(o => o.CreatedAt) // Sắp xếp tăng dần là bắt buộc
                                            .ToListAsync();

            if (!allCustomerOrders.Any())
            {
                return new List<OrderListDto>();
            }

            var resultList = new List<OrderListDto>();
            var currentGroup = new List<Order>();

            // Bắt đầu nhóm đầu tiên với đơn hàng đầu tiên
            currentGroup.Add(allCustomerOrders.First());

            // 2. Lặp qua các đơn hàng còn lại để gom nhóm
            for (int i = 1; i < allCustomerOrders.Count; i++)
            {
                var currentOrder = allCustomerOrders[i];
                var previousOrderInGroup = currentGroup.Last(); // So sánh với đơn hàng cuối cùng trong nhóm hiện tại

                // 3. Kiểm tra xem thời gian tạo có gần nhau không (ví dụ: dưới 5 giây)
                var timeDifferenceInSeconds = (currentOrder.CreatedAt - previousOrderInGroup.CreatedAt).TotalSeconds;

                if (timeDifferenceInSeconds < 5)
                {
                    // Nếu gần nhau, thêm vào nhóm hiện tại
                    currentGroup.Add(currentOrder);
                }
                else
                {
                    // Nếu không, đây là một lần thanh toán mới.
                    // Xử lý và đóng gói nhóm cũ...
                    resultList.Add(CombineOrderGroup(currentGroup));

                    // ...và bắt đầu một nhóm hoàn toàn mới với đơn hàng hiện tại
                    currentGroup = new List<Order> { currentOrder };
                }
            }

            // 4. Xử lý nhóm cuối cùng còn lại trong danh sách sau khi vòng lặp kết thúc
            if (currentGroup.Any())
            {
                resultList.Add(CombineOrderGroup(currentGroup));
            }

            // Sắp xếp lại kết quả cuối cùng theo thứ tự mới nhất lên đầu để hiển thị
            return resultList.OrderByDescending(o => o.CreatedAt);
        }

        // Thêm hàm pomocniczy (helper method) này vào trong cùng class OrderService
        private OrderListDto CombineOrderGroup(List<Order> orderGroup)
        {
            if (orderGroup == null || !orderGroup.Any()) return null;

            // Lấy đơn hàng đầu tiên trong nhóm làm thông tin đại diện
            var representativeOrder = orderGroup.First();

            // Gộp tất cả sản phẩm từ các đơn hàng trong nhóm lại
            var allItemsInGroup = orderGroup.SelectMany(o => o.Items).ToList();

            // Tính tổng số tiền của cả nhóm
            var groupTotalAmount = orderGroup.Sum(o => o.TotalAmount);

            // Dùng AutoMapper để map các thông tin chung từ đơn hàng đại diện
            var combinedOrderDto = _mapper.Map<OrderListDto>(representativeOrder);

            // Ghi đè các thông tin đã được gộp
            combinedOrderDto.Items = _mapper.Map<List<OrderItemListDto>>(allItemsInGroup);
            combinedOrderDto.TotalAmount = groupTotalAmount;

            // Tạo một mã định danh chung cho "lần thanh toán" này
            // Bạn có thể dùng Id của đơn hàng đầu tiên hoặc TransactionId nếu có
            combinedOrderDto.Id = representativeOrder.Id;
            combinedOrderDto.OrderCode = $"{representativeOrder.CreatedAt:yyMMddHHmm}";

            return combinedOrderDto;
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
                                         .OrderByDescending(o => o.CreatedAt)
                                         .ToListAsync();

            return _mapper.Map<IEnumerable<OrderListDto>>(orders);
        }

        public async Task<Order> GetOrderEntityByIdAsync(Guid orderId)
        {
            return await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<OrderDetailsDto> GetOrderDetailsForProviderAsync(Guid orderId)
        {
            try
            {
                var order = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images.Where(i => i.IsPrimary))
                    .Include(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                    .Include(o => o.DiscountCode)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return null;
                }

                var orderDetailsDto = _mapper.Map<OrderDetailsDto>(order);

                // Violation summary (provider view)
                var orderItemIds = order.Items.Select(i => i.Id).ToList();
                var violationQuery = _context.RentalViolations
                    .AsNoTracking()
                    .Where(v => orderItemIds.Contains(v.OrderItemId));
                orderDetailsDto.HasReturnIssues = await violationQuery.AnyAsync();
                if (orderDetailsDto.HasReturnIssues)
                {
                    orderDetailsDto.TotalPenaltyAmount = await violationQuery.SumAsync(v => v.PenaltyAmount);
                }

                // Fetch payment info from Transaction for provider
                var transaction = await _context.Transactions
                    .AsNoTracking()
                    .Where(t => t.Orders.Any(o => o.Id == orderId) && t.Status == TransactionStatus.completed)
                    .OrderByDescending(t => t.TransactionDate)
                    .FirstOrDefaultAsync();
                    
                if (transaction != null)
                {
                    orderDetailsDto.PaymentMethod = transaction.PaymentMethod;
                    orderDetailsDto.PaymentConfirmedDate = transaction.TransactionDate;
                }
                
                return orderDetailsDto;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOrderDetailsForProviderAsync for orderId {orderId}: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new Exception($"Failed to get order details for provider: {ex.Message}", ex);
            }
        }

        public async Task<OrderDetailsDto> GetOrderDetailsAsync(Guid orderId)
        {
            try
            {
                // Reduce tracking and payload size: AsNoTracking + filtered includes
                var order = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images.Where(i => i.IsPrimary))
                    .Include(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                    .Include(o => o.DiscountCode) // Include discount code để map tên
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return null;
                }

                var orderDetailsDto = _mapper.Map<OrderDetailsDto>(order);

                // Violation summary for customer view (to surface penalties even after resolve)
                var orderItemIds = order.Items.Select(i => i.Id).ToList();
                var violationQuery = _context.RentalViolations
                    .AsNoTracking()
                    .Where(v => orderItemIds.Contains(v.OrderItemId));
                orderDetailsDto.HasReturnIssues = await violationQuery.AnyAsync();
                if (orderDetailsDto.HasReturnIssues)
                {
                    orderDetailsDto.TotalPenaltyAmount = await violationQuery.SumAsync(v => v.PenaltyAmount);
                }

                // Fetch latest payment method without loading entire transactions collection
                var latestPaymentMethod = await _context.Transactions
                    .AsNoTracking()
                    .Where(t => t.Orders.Any(o => o.Id == orderId))
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => t.PaymentMethod)
                    .FirstOrDefaultAsync();
                    
                if (!string.IsNullOrEmpty(latestPaymentMethod))
                {
                    orderDetailsDto.PaymentMethod = latestPaymentMethod;
                }
                
                return orderDetailsDto;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetOrderDetailsAsync for orderId {orderId}: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new Exception($"Failed to get order details: {ex.Message}", ex);
            }
        }
        public async Task ClearCartItemsForOrderAsync(Order order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
            {
                return;
            }

            var customerId = order.CustomerId;
            var productIdsInOrder = order.Items.Select(i => i.ProductId).ToList();

            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null || !cart.Items.Any())
            {
                return;
            }

            var cartItemsToRemove = cart.Items
                .Where(ci => productIdsInOrder.Contains(ci.ProductId))
                .ToList();

            if (cartItemsToRemove.Any())
            {
                foreach (var item in cartItemsToRemove)
                {
                    // Giả sử _cartRepository có phương thức xóa dựa trên CartItem entity
                    await _cartRepository.DeleteCartItemAsync(item);
                }
            }
        }

        public async Task<Guid> RentAgainOrderAsync(Guid customerId, RentAgainRequestDto requestDto)
        {
            var originalOrder = await _orderRepo.GetOrderWithItemsAsync(requestDto.OriginalOrderId);

            if (originalOrder == null)
            {
                throw new Exception("Original order not found.");
            }

            if (originalOrder.CustomerId != customerId)
            {
                throw new UnauthorizedAccessException("You are not authorized to rent this order again.");
            }

            if (originalOrder.Status != OrderStatus.returned)
            {
                throw new InvalidOperationException("Only returned orders can be rented again.");
            }

            if (requestDto.NewRentalStartDate >= requestDto.NewRentalEndDate)
            {
                throw new ArgumentException("New rental end date must be after start date.");
            }

            // Check product availability for new dates
            foreach (var item in originalOrder.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null)
                {
                    throw new Exception($"Product with ID {item.ProductId} not found.");
                }

                var isAvailable = await _productRepo.IsProductAvailable(item.ProductId, requestDto.NewRentalStartDate, requestDto.NewRentalEndDate);
                if (!isAvailable)
                {
                    throw new InvalidOperationException($"Product '{product.Name}' is not available for the selected dates.");
                }
            }

            // Create a new order based on the old one
            var newOrder = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = originalOrder.CustomerId,
                ProviderId = originalOrder.ProviderId,
                Status = OrderStatus.pending,
                RentalStart = requestDto.NewRentalStartDate,
                RentalEnd = requestDto.NewRentalEndDate,
                TotalAmount = 0,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
            };

            int calculatedRentalDays = Math.Max(1, (int)(requestDto.NewRentalEndDate - requestDto.NewRentalStartDate).TotalDays);

            foreach (var originalItem in originalOrder.Items)
            {
                var newItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = newOrder.Id,
                    ProductId = originalItem.ProductId,
                    Quantity = originalItem.Quantity,
                    DailyRate = originalItem.DailyRate,
                    RentalDays = calculatedRentalDays
                };
                newOrder.Items.Add(newItem);
            }

            newOrder.TotalAmount = newOrder.Items.Sum(item =>
                item.DailyRate * item.Quantity * calculatedRentalDays);
            
            // Set subtotal equal to total amount (rental fees only, no additional charges)
            newOrder.Subtotal = newOrder.TotalAmount;

            await _orderRepo.AddAsync(newOrder);

            await _notificationService.NotifyNewOrderCreated(newOrder.Id);

            // Send notifications to both parties
            await _hubContext.Clients.Group($"notifications-{newOrder.CustomerId}")
            .SendAsync("ReceiveNotification", $"New order #{newOrder.Id} created (Status: {newOrder.Status})");

            await _hubContext.Clients.Group($"notifications-{newOrder.ProviderId}")
                .SendAsync("ReceiveNotification", $"New order #{newOrder.Id} received (Status: {newOrder.Status})");

            return newOrder.Id;
        }

        public async Task<bool> UpdateOrderContactInfoAsync(Guid customerId, UpdateOrderContactInfoDto dto)
        {
            var order = await _orderRepo.GetByIdAsync(dto.OrderId);

            if (order == null)
            {
                return false;
            }

            if (order.CustomerId != customerId)
            {
                return false;
            }

            // Chỉ cho cập nhật nếu đơn hàng đang ở trạng thái "pending"
            if (order.Status != OrderStatus.pending)
            {
                return false;
            }

            order.CustomerFullName = dto.CustomerFullName;
            order.CustomerEmail = dto.CustomerEmail;
            order.CustomerPhoneNumber = dto.CustomerPhoneNumber;
            order.DeliveryAddress = dto.DeliveryAddress;
            order.HasAgreedToPolicies = dto.HasAgreedToPolicies;
            order.UpdatedAt = DateTimeHelper.GetVietnamTime();

            return await _orderRepo.UpdateOrderContactInfoAsync(order);
        }

        /// <summary>
        /// Updates subtotal for all orders that have subtotal = 0
        /// This is a utility method to fix existing orders
        /// </summary>
        public async Task UpdateOrderSubtotalsAsync()
        {
            var orders = await _context.Orders
                .Where(o => o.Subtotal == 0)
                .Include(o => o.Items)
                .ToListAsync();

            foreach (var order in orders)
            {
                if (order.Items.Any())
                {
                    order.Subtotal = order.Items.Sum(item => 
                        item.TransactionType == BusinessObject.Enums.TransactionType.purchase
                            ? item.DailyRate * item.Quantity
                            : item.DailyRate * (item.RentalDays ?? 1) * item.Quantity);
                    order.UpdatedAt = DateTimeHelper.GetVietnamTime();
                }
            }

            await _context.SaveChangesAsync();
        }

        public Task<string> GetOrderItemId(Guid customerId, Guid productId)
        {
            return _orderRepo.GetOrderItemId(customerId, productId);
        }

        /// <summary>
        /// Updates product rent count, buy count, and stock quantities based on order status
        /// - Stock quantities (RentalQuantity/PurchaseQuantity) are deducted when order becomes 'approved' (payment confirmed)
        /// - Rent count increases whenever order status becomes 'returned' (including returned_with_issue -> returned)
        /// - Buy count increases only when purchase order first reaches 'approved'
        /// - Uses pessimistic locking to prevent race conditions during stock deduction
        /// </summary>
        /// <param name="order">The order containing items to update counts for</param>
        /// <param name="oldStatus">The previous order status</param>
        /// <param name="newStatus">The new order status</param>
        private async Task UpdateProductCounts(Order order, OrderStatus oldStatus, OrderStatus newStatus)
        {
            // Group items by ProductId to handle multiple items of same product in one order
            var itemGroups = order.Items.GroupBy(i => i.ProductId);

            foreach (var itemGroup in itemGroups)
            {
                var productId = itemGroup.Key;
                var items = itemGroup.ToList();
                
                // Calculate total quantities for this product across all items in the order
                var totalPurchaseQuantity = items
                    .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                    .Sum(i => i.Quantity);
                var totalRentalQuantity = items
                    .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental)
                    .Sum(i => i.Quantity);

                bool shouldDeductStock = newStatus == OrderStatus.approved && oldStatus != OrderStatus.approved;
                bool shouldUpdateCounts = false;

                // Check if we need to update rent/buy counts
                foreach (var item in items)
                {
                    if (item.TransactionType == BusinessObject.Enums.TransactionType.rental && newStatus == OrderStatus.returned)
                    {
                        shouldUpdateCounts = true;
                    }
                    else if (item.TransactionType == BusinessObject.Enums.TransactionType.purchase && 
                             newStatus == OrderStatus.approved && oldStatus != OrderStatus.approved)
                    {
                        shouldUpdateCounts = true;
                    }
                }

                // If we need to deduct stock (critical section), use database locking
                if (shouldDeductStock && (totalPurchaseQuantity > 0 || totalRentalQuantity > 0))
                {
                    // Use raw SQL with UPDLOCK for SQL Server row locking
                    var lockedProduct = await _context.Products
                        .FromSqlRaw("SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", productId)
                        .FirstOrDefaultAsync();

                    if (lockedProduct == null)
                    {
                        throw new InvalidOperationException($"Product with ID {productId} not found during stock deduction.");
                    }

                    // Validate stock availability with locked data
                    if (totalPurchaseQuantity > 0)
                    {
                        if (lockedProduct.PurchaseQuantity < totalPurchaseQuantity)
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock for purchase. Product '{lockedProduct.Name}' has {lockedProduct.PurchaseQuantity} units available, but {totalPurchaseQuantity} units requested.");
                        }
                        lockedProduct.PurchaseQuantity -= totalPurchaseQuantity;
                    }

                    if (totalRentalQuantity > 0)
                    {
                        if (lockedProduct.RentalQuantity < totalRentalQuantity)
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock for rental. Product '{lockedProduct.Name}' has {lockedProduct.RentalQuantity} units available, but {totalRentalQuantity} units requested.");
                        }
                        lockedProduct.RentalQuantity -= totalRentalQuantity;
                    }

                    // Update buy/rent counts if needed
                    if (shouldUpdateCounts)
                    {
                        foreach (var item in items)
                        {
                            if (item.TransactionType == BusinessObject.Enums.TransactionType.purchase && 
                                newStatus == OrderStatus.approved && oldStatus != OrderStatus.approved)
                            {
                                lockedProduct.BuyCount += item.Quantity;
                            }
                        }
                    }

                    // Save changes while still holding the lock
                    _context.Products.Update(lockedProduct);
                    await _context.SaveChangesAsync();
                }
                // If we only need to update counts (no stock deduction), use regular update
                else if (shouldUpdateCounts)
                {
                    var product = await _productRepository.GetByIdAsync(productId);
                    if (product != null)
                    {
                        foreach (var item in items)
                        {
                            if (item.TransactionType == BusinessObject.Enums.TransactionType.rental && newStatus == OrderStatus.returned)
                            {
                                product.RentCount += item.Quantity;
                            }
                            else if (item.TransactionType == BusinessObject.Enums.TransactionType.purchase && 
                                     newStatus == OrderStatus.approved && oldStatus != OrderStatus.approved)
                            {
                                product.BuyCount += item.Quantity;
                            }
                        }
                        await _productRepository.UpdateAsync(product);
                    }
                }
            }
        }

        public async Task<IEnumerable<AdminOrderListDto>> GetAllOrdersForAdminAsync()
        {
            // OPTIMIZED: Load orders and paid order IDs separately (faster than Include)
            var orders = await _orderRepo.GetAllOrdersBasicAsync();
            var paidOrderIds = await _orderRepo.GetPaidOrderIdsAsync();
            
            var result = new List<AdminOrderListDto>();
            
            foreach (var order in orders)
            {
                // Use consistent logic with GetOrderDetailForAdminAsync
                var transactionType = DetermineTransactionType(order);
                
                // Calculate total discount (all types)
                var totalDiscount = order.DiscountAmount + order.ItemRentalCountDiscount + order.LoyaltyDiscount;
                
                // TotalAmount = Subtotal - all discounts (NOT including deposit)
                var displayTotal = order.Subtotal - totalDiscount;
                
                // Check if payment is completed using pre-loaded set
                var isPaid = paidOrderIds.Contains(order.Id);
                
                // If order is determined as rental, only create rental entry
                if (transactionType == "rental")
                {
                    result.Add(new AdminOrderListDto
                    {
                        Id = order.Id,
                        OrderCode = GenerateOrderCode(order) + "-R",
                        CustomerName = order.Customer?.Profile?.FullName ?? order.CustomerFullName ?? "N/A",
                        CustomerEmail = order.Customer?.Email ?? order.CustomerEmail ?? "N/A",
                        CustomerAvatar = order.Customer?.Profile?.ProfilePictureUrl,
                        ProviderName = order.Provider?.Profile?.FullName ?? "N/A",
                        ProviderEmail = order.Provider?.Email ?? "N/A",
                        ProviderAvatar = order.Provider?.Profile?.ProfilePictureUrl,
                        TransactionType = "rental",
                        RentalStartDate = order.RentalStart,
                        RentalEndDate = order.RentalEnd,
                        Status = order.Status,
                        TotalAmount = displayTotal,
                        Subtotal = order.Subtotal,
                        DiscountAmount = totalDiscount,
                        CreatedAt = order.CreatedAt,
                        IsPaid = isPaid
                    });
                }
                // If order is determined as purchase, only create purchase entry
                else
                {
                    result.Add(new AdminOrderListDto
                    {
                        Id = order.Id,
                        OrderCode = GenerateOrderCode(order) + "-P",
                        CustomerName = order.Customer?.Profile?.FullName ?? order.CustomerFullName ?? "N/A",
                        CustomerEmail = order.Customer?.Email ?? order.CustomerEmail ?? "N/A",
                        CustomerAvatar = order.Customer?.Profile?.ProfilePictureUrl,
                        ProviderName = order.Provider?.Profile?.FullName ?? "N/A",
                        ProviderEmail = order.Provider?.Email ?? "N/A",
                        ProviderAvatar = order.Provider?.Profile?.ProfilePictureUrl,
                        TransactionType = "purchase",
                        RentalStartDate = null,
                        RentalEndDate = null,
                        Status = order.Status,
                        TotalAmount = displayTotal,
                        Subtotal = order.Subtotal,
                        DiscountAmount = totalDiscount,
                        CreatedAt = order.CreatedAt,
                        IsPaid = isPaid
                    });
                }
            }
            
            return result.OrderByDescending(o => o.CreatedAt).ToList();
        }

        private string GenerateOrderCode(Order order)
        {
            return $"ORD-{order.Id.ToString().Substring(0, 8).ToUpper()}";
        }

        private string DetermineTransactionType(Order order)
        {
            // Check if order has rental dates
            if (order.RentalStart.HasValue && order.RentalEnd.HasValue)
                return "rental";
            
            // Check order items transaction type
            if (order.Items != null && order.Items.Any())
            {
                var firstItem = order.Items.First();
                return firstItem.TransactionType == BusinessObject.Enums.TransactionType.rental ? "rental" : "purchase";
            }
            
            return "purchase"; // default
        }

        public async Task<AdminOrderDetailDto> GetOrderDetailForAdminAsync(Guid orderId)
        {
            var order = await _orderRepo.GetOrderWithFullDetailsAsync(orderId);
            
            if (order == null)
                return null;

            var transactionType = DetermineTransactionType(order);
            var rentalDays = order.RentalStart.HasValue && order.RentalEnd.HasValue 
                ? (int)(order.RentalEnd.Value - order.RentalStart.Value).TotalDays 
                : (int?)null;

            return new AdminOrderDetailDto
            {
                Id = order.Id,
                OrderCode = GenerateOrderCode(order),
                Status = order.Status,
                TransactionType = transactionType,
                CreatedAt = order.CreatedAt,
                
                // Customer Information
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.Profile?.FullName ?? order.CustomerFullName ?? "N/A",
                CustomerEmail = order.Customer?.Email ?? order.CustomerEmail ?? "N/A",
                CustomerPhone = order.CustomerPhoneNumber ?? order.Customer?.Profile?.Phone ?? "N/A",
                CustomerAddress = order.DeliveryAddress ?? "N/A",
                
                // Provider Information
                ProviderId = order.ProviderId,
                ProviderName = order.Provider?.Profile?.FullName ?? "N/A",
                ProviderEmail = order.Provider?.Email ?? "N/A",
                ProviderPhone = order.Provider?.Profile?.Phone ?? "N/A",
                
                // Rental Information
                RentalStartDate = order.RentalStart,
                RentalEndDate = order.RentalEnd,
                RentalDays = rentalDays,
                
                // Order Items
                OrderItems = order.Items?.Select(item => new AdminOrderItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product?.Name ?? "N/A",
                    ProductImage = item.Product?.Images?.FirstOrDefault()?.ImageUrl ?? "",
                    Color = item.Product?.Color ?? "N/A",
                    Quantity = item.Quantity,
                    UnitPrice = item.DailyRate,
                    TotalPrice = item.TransactionType == BusinessObject.Enums.TransactionType.purchase
                        ? item.DailyRate * item.Quantity
                        : item.DailyRate * (item.RentalDays ?? 1) * item.Quantity,
                    TransactionType = item.TransactionType == BusinessObject.Enums.TransactionType.rental ? "rental" : "purchase",
                    TotalDeposit = item.TransactionType == BusinessObject.Enums.TransactionType.rental 
                        ? item.DepositPerUnit * item.Quantity 
                        : null,
                    RentalDays = item.RentalDays
                }).ToList() ?? new List<AdminOrderItemDto>(),
                
                // Financial Information
                Subtotal = order.Subtotal,
                ShippingFee = 0,
                DiscountAmount = order.DiscountAmount,
                ItemRentalCountDiscount = order.ItemRentalCountDiscount,
                LoyaltyDiscount = order.LoyaltyDiscount,
                ItemRentalCountDiscountPercent = order.ItemRentalCountDiscountPercent,
                LoyaltyDiscountPercent = order.LoyaltyDiscountPercent,
                TotalCommission = order.Items?.Sum(i => i.CommissionAmount) ?? 0,
                TotalAmount = order.TotalAmount,
                
                // Payment Information
                PaymentMethod = "N/A",
                IsPaid = order.Status == OrderStatus.approved || order.Status == OrderStatus.in_transit || order.Status == OrderStatus.in_use || order.Status == OrderStatus.returned,
                
                // Additional Information
                Note = null,
                UpdatedAt = order.UpdatedAt
            };
        }

        /// <summary>
        /// Format order status text for display in notifications
        /// </summary>
        private string FormatOrderStatusText(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.pending => "Pending",
                OrderStatus.approved => "Paid",
                OrderStatus.in_transit => "In Transit",
                OrderStatus.in_use => "In Use",
                OrderStatus.returning => "Returning",
                OrderStatus.returned => "Returned",
                OrderStatus.returned_with_issue => "Returned with Issue",
                OrderStatus.cancelled => "Cancelled",
                _ => status.ToString()
            };
        }
    }
}