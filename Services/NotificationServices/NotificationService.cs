using BusinessObject.Enums;
using BusinessObject.Models;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Repositories.NotificationRepositories;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public async Task<IEnumerable<Notification>> GetUserNotifications(Guid userId, bool unreadOnly = false)
        {
            if (unreadOnly)
            {
                return await _notificationRepository.GetUnreadByUserIdAsync(userId);
            }

            return await _notificationRepository.GetByUserIdAsync(userId);
        }

        public async Task SendNotification(Guid userId, string message, NotificationType type)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow,
                OrderId = orderId
            };

            await _notificationRepository.AddAsync(notification);

            // Send real-time notification
            await _hubContext.Clients.Group($"notifications-{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Message,
                    notification.Type,
                    notification.CreatedAt,
                    notification.OrderId
                });
        }

        public async Task NotifyTransactionCompleted(Guid orderId, Guid userId)
        {
            var message = $"Order #{orderId} transaction has been completed.";
            // Giả sử có lưu notification vào DB
            await _notificationRepository.AddAsync(new Notification
            {
                OrderId = orderId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
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
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            });
        }
    }
}
