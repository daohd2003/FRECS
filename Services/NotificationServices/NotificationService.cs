using BusinessObject.DTOs.NotificationDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.NotificationRepositories;
using Repositories.RepositoryBase;
using BusinessObject.Utilities;

namespace Services.NotificationServices
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IRepository<Order> _orderRepository;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(INotificationRepository notificationRepository, IRepository<Order> orderRepository, IHubContext<NotificationHub> hubContext)
        {
            _notificationRepository = notificationRepository;
            _orderRepository = orderRepository;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<NotificationResponse>> GetUserNotifications(Guid userId, bool unreadOnly = false)
        {
            // 1. Lấy danh sách các đối tượng Notification gốc từ repository
            IEnumerable<Notification> notifications;
            if (unreadOnly)
            {
                notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId);
            }
            else
            {
                notifications = await _notificationRepository.GetByUserIdAsync(userId);
            }

            if (notifications == null || !notifications.Any())
            {
                return Enumerable.Empty<NotificationResponse>();
            }

            // 2. Chuyển đổi (Map) từ Notification sang NotificationResponse
            var notificationResponses = notifications.Select(n => new NotificationResponse
            {
                Id = n.Id,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                Type = n.Type,
                OrderId = n.OrderId
                // Tạo link URL động dựa trên loại thông báo và OrderId
                /*LinkUrl = GenerateNotificationLink(n.Type, n.OrderId)*/
            });

            return notificationResponses;
        }

        public async Task SendNotification(Guid userId, string message, NotificationType type)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTimeHelper.GetVietnamTime()
            };

            await _notificationRepository.AddAsync(notification);
        }

        public async Task<int> GetUnreadCount(Guid userId)
        {
            return await _notificationRepository.GetUnreadCountByUserIdAsync(userId);
        }

        public async Task MarkAsRead(Guid notificationId)
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _notificationRepository.UpdateAsync(notification);
            }
        }

        public async Task MarkAllAsRead(Guid userId)
        {
            await _notificationRepository.MarkAllAsReadByUserIdAsync(userId);
        }

        public async Task NotifyOrderStatusChange(Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            var message = $"Order #{orderId.ToString().Substring(0, 8)} status changed from {oldStatus} to {newStatus}";

            // Notify customer
            await CreateAndSendNotification(
                order.CustomerId,
                message,
                NotificationType.order,
                orderId);

            // Notify provider
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
            // Giả sử có lưu notification vào DB
            await _notificationRepository.AddAsync(new Notification
            {
                OrderId = orderId,
                Message = message,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                UserId = userId
            });
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
