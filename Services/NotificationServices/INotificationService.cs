using BusinessObject.DTOs.NotificationDto;
using BusinessObject.DTOs.PagingDto;
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
        Task<IEnumerable<NotificationResponse>> GetUserNotifications(Guid userId, bool unreadOnly = false);
        Task<PagedResult<NotificationResponse>> GetPagedNotifications(
            Guid userId,
            int page,
            int pageSize,
            string? searchTerm = null,
            NotificationType? filterType = null,
            bool? isRead = null);
        Task MarkAsRead(Guid notificationId);
        Task MarkAllAsRead(Guid userId);
        Task SendNotification(Guid userId, string message, NotificationType type, Guid? orderId = null);
        Task<int> GetUnreadCount(Guid userId);
        Task NotifyOrderStatusChange(Guid orderId, OrderStatus oldStatus, OrderStatus newStatus);
        Task NotifyNewOrderCreated(Guid orderId);
        Task NotifyOrderCancellation(Guid orderId);
        Task NotifyOrderItemsUpdate(Guid orderId, IEnumerable<Guid> updatedItemIds);
        Task NotifyTransactionCompleted(Guid orderId, Guid userId);
        Task NotifyTransactionFailed(Guid orderId, Guid userId);
        Task NotifyTransactionFailedByTransactionId(Guid transactionId, Guid userId);
        Task DeleteNotification(Guid notificationId);
    }
}
