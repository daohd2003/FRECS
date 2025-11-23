using BusinessObject.DTOs.NotificationDto;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.NotificationRepositories;
using Repositories.RepositoryBase;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.NotificationServices
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IRepository<Order> _orderRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ShareItDbContext _context;

        public NotificationService(INotificationRepository notificationRepository, IRepository<Order> orderRepository, IHubContext<NotificationHub> hubContext, ShareItDbContext context)
        {
            _notificationRepository = notificationRepository;
            _orderRepository = orderRepository;
            _hubContext = hubContext;
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách thông báo của user (tất cả hoặc chỉ chưa đọc)
        /// Xác định user là customer hay provider trong từng thông báo để hiển thị phù hợp
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="unreadOnly">true = chỉ lấy chưa đọc, false = lấy tất cả</param>
        /// <returns>Danh sách NotificationResponse</returns>
        public async Task<IEnumerable<NotificationResponse>> GetUserNotifications(Guid userId, bool unreadOnly = false)
        {
            // Bước 1: Lấy danh sách thông báo từ repository
            IEnumerable<Notification> notifications;
            if (unreadOnly)
            {
                // Chỉ lấy thông báo chưa đọc
                notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId);
            }
            else
            {
                // Lấy tất cả thông báo
                notifications = await _notificationRepository.GetByUserIdAsync(userId);
            }

            if (notifications == null || !notifications.Any())
            {
                return Enumerable.Empty<NotificationResponse>();
            }

            // Bước 2: Chuyển đổi Entity sang DTO và xác định role của user
            var notificationResponses = new List<NotificationResponse>();
            
            foreach (var n in notifications)
            {
                bool? isUserProvider = null;
                
                // Nếu thông báo liên quan đến đơn hàng, xác định user là customer hay provider
                // Mục đích: Hiển thị thông báo khác nhau cho customer và provider
                if (n.OrderId.HasValue && n.OrderId.Value != Guid.Empty)
                {
                    var order = await _context.Orders
                        .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                        .FirstOrDefaultAsync(o => o.Id == n.OrderId.Value);
                    
                    if (order != null)
                    {
                        // Kiểm tra user có phải là provider của đơn hàng không
                        // (tất cả items trong cùng 1 order thường cùng 1 provider)
                        var firstItem = order.Items.FirstOrDefault();
                        if (firstItem?.Product != null)
                        {
                            isUserProvider = (firstItem.Product.ProviderId == userId);
                        }
                    }
                }
                
                // Tạo NotificationResponse với thông tin đầy đủ
                notificationResponses.Add(new NotificationResponse
                {
                    Id = n.Id,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    Type = n.Type,
                    OrderId = n.OrderId,
                    IsUserProvider = isUserProvider // null nếu không liên quan đến order
                });
            }

            return notificationResponses;
        }

        /// <summary>
        /// Gửi thông báo cho một user cụ thể
        /// Lưu vào database để user có thể xem lại sau
        /// </summary>
        /// <param name="userId">ID người nhận thông báo</param>
        /// <param name="message">Nội dung thông báo</param>
        /// <param name="type">Loại thông báo (order, system, message, content_violation, etc.)</param>
        /// <param name="orderId">ID đơn hàng liên quan (nếu có)</param>
        public async Task SendNotification(Guid userId, string message, NotificationType type, Guid? orderId = null)
        {
            // Tạo notification entity
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                Type = type,
                IsRead = false, // Mặc định chưa đọc
                OrderId = orderId, // Liên kết với đơn hàng (nếu có)
                CreatedAt = DateTimeHelper.GetVietnamTime()
            };

            // Lưu vào database
            await _notificationRepository.AddAsync(notification);
        }

        public async Task<int> GetUnreadCount(Guid userId)
        {
            return await _notificationRepository.GetUnreadCountByUserIdAsync(userId);
        }

        /// <summary>
        /// Đánh dấu một thông báo là đã đọc
        /// Dùng khi user click vào thông báo
        /// </summary>
        /// <param name="notificationId">ID thông báo</param>
        public async Task MarkAsRead(Guid notificationId)
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _notificationRepository.UpdateAsync(notification);
            }
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo của user là đã đọc
        /// Dùng khi user click "Mark all as read"
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        public async Task MarkAllAsRead(Guid userId)
        {
            await _notificationRepository.MarkAllAsReadByUserIdAsync(userId);
        }

        /// <summary>
        /// Gửi thông báo khi trạng thái đơn hàng thay đổi
        /// Thông báo cho cả customer và provider về thay đổi
        /// </summary>
        /// <param name="orderId">ID đơn hàng</param>
        /// <param name="oldStatus">Trạng thái cũ</param>
        /// <param name="newStatus">Trạng thái mới</param>
        public async Task NotifyOrderStatusChange(Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
        {
            // Lấy thông tin đơn hàng
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            // Tạo nội dung thông báo
            var message = $"Order #{orderId.ToString().Substring(0, 8)} status changed from {oldStatus} to {newStatus}";

            // Gửi thông báo cho customer (người mua/thuê)
            await CreateAndSendNotification(
                order.CustomerId,
                message,
                NotificationType.order,
                orderId);

            // Gửi thông báo cho provider (người bán/cho thuê)
            await CreateAndSendNotification(
                order.ProviderId,
                message,
                NotificationType.order,
                orderId);
        }

        public async Task NotifyNewOrderCreated(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            var message = $"New order #{orderId.ToString().Substring(0, 8)} has been created";

            // Only notify provider about new pending orders
            await CreateAndSendNotification(
                order.ProviderId,
                message,
                NotificationType.order,
                orderId);
        }

        public async Task NotifyOrderCancellation(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            var message = $"Order #{orderId.ToString().Substring(0, 8)} has been cancelled";

            // Notify both parties
            await CreateAndSendNotification(
                order.CustomerId,
                message,
                NotificationType.order,
                orderId);

            await CreateAndSendNotification(
                order.ProviderId,
                message,
                NotificationType.order,
                orderId);
        }

        public async Task NotifyOrderItemsUpdate(Guid orderId, IEnumerable<Guid> updatedItemIds)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            var message = $"Order #{orderId.ToString().Substring(0, 8)} items have been updated";

            // Notify both parties
            await CreateAndSendNotification(
                order.CustomerId,
                message,
                NotificationType.order,
                orderId);

            await CreateAndSendNotification(
                order.ProviderId,
                message,
                NotificationType.order,
                orderId);
        }

        /// <summary>
        /// Helper method: Tạo và lưu thông báo vào database
        /// Dùng nội bộ để tránh code trùng lặp
        /// </summary>
        /// <param name="userId">ID người nhận</param>
        /// <param name="message">Nội dung thông báo</param>
        /// <param name="type">Loại thông báo</param>
        /// <param name="orderId">ID đơn hàng liên quan</param>
        private async Task CreateAndSendNotification(Guid userId, string message, NotificationType type, Guid orderId)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                OrderId = orderId
            };

            await _notificationRepository.AddAsync(notification);
        }

        public async Task NotifyTransactionCompleted(Guid orderId, Guid userId)
        {
            var message = $"Order #{orderId} transaction has been completed.";
            // Giả sử có lưu notification vào DB
            await _notificationRepository.AddAsync(new Notification
            {
                OrderId = orderId,
                Message = message,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                UserId = userId
            });
        }

        public async Task NotifyTransactionFailed(Guid orderId, Guid userId)
        {
            var message = $"Order #{orderId} transaction has been failed.";
            
            // Save notification to DB
            var notification = new Notification
            {
                OrderId = orderId,
                Message = message,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                UserId = userId,
                Type = NotificationType.order,
                IsRead = false
            };
            
            await _notificationRepository.AddAsync(notification);
            
            // Send realtime notification via SignalR
            await _hubContext.Clients.Group($"notifications-{userId}")
                .SendAsync("ReceiveNotification", message);
        }

        public async Task NotifyTransactionFailedByTransactionId(Guid transactionId, Guid userId)
        {
            // Query transaction with orders from database
            var transaction = await _context.Transactions
                .Include(t => t.Orders)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction != null && transaction.Orders != null && transaction.Orders.Any())
            {
                // Send notification for each order in the transaction
                foreach (var order in transaction.Orders)
                {
                    await NotifyTransactionFailed(order.Id, userId);
                }
            }
        }

        public async Task<PagedResult<NotificationResponse>> GetPagedNotifications(
            Guid userId,
            int page,
            int pageSize,
            string? searchTerm = null,
            NotificationType? filterType = null,
            bool? isRead = null)
        {
            var (items, totalCount) = await _notificationRepository.GetPagedNotificationsAsync(
                userId, page, pageSize, searchTerm, filterType, isRead);

            var notificationResponses = items.Select(n => new NotificationResponse
            {
                Id = n.Id,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                Type = n.Type,
                OrderId = n.OrderId
            }).ToList();

            return new PagedResult<NotificationResponse>
            {
                Items = notificationResponses,
                TotalCount = totalCount,
                PageSize = pageSize,
                CurrentPage = page
            };
        }

        public async Task DeleteNotification(Guid notificationId)
        {
            await _notificationRepository.DeleteNotificationAsync(notificationId);
        }

        /*/// <summary>
        /// Hàm trợ giúp để tạo link điều hướng cho thông báo.
        /// </summary>
        private string? GenerateNotificationLink(NotificationType type, Guid? orderId)
        {
            // Bạn có thể mở rộng switch case này cho các loại thông báo khác
            switch (type)
            {
                case NotificationType.system:
                case NotificationType.message:
                case NotificationType.order:
                    return orderId.HasValue ? $"/MyOrders/Details/{orderId.Value}" : null;

                case NotificationType.NewMessage:
                    return "/Messages"; // Hoặc "/Messages/{conversationId}" nếu có

                case NotificationType.Promotion:
                    return "/Promotions";

                default:
                    return null; // Không có link cho các loại thông báo chung
            }
        }*/
    }
}
