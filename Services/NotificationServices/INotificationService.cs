using BusinessObject.Enums;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.NotificationServices
{
    public interface INotificationService
    {
        Task<IEnumerable<Notification>> GetUserNotifications(Guid userId, bool unreadOnly = false);
        Task MarkAsRead(Guid notificationId);
        Task MarkAllAsRead(Guid userId);
        Task SendNotification(Guid userId, string message, NotificationType type);
        Task<int> GetUnreadCount(Guid userId);
        Task NotifyOrderStatusChange(Guid orderId, OrderStatus oldStatus, OrderStatus newStatus);
        Task NotifyNewOrderCreated(Guid orderId);
        Task NotifyOrderCancellation(Guid orderId);
        Task NotifyOrderItemsUpdate(Guid orderId, IEnumerable<Guid> updatedItemIds);
        Task NotifyTransactionCompleted(Guid orderId, Guid userId);
        Task NotifyTransactionFailed(Guid orderId, Guid userId);
    }
}
