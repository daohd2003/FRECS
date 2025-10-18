using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.NotificationDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.NotificationServices;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // GET: api/notification/user/{userId}?unreadOnly=true
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserNotifications(Guid userId, [FromQuery] bool unreadOnly = false)
        {
            var notifications = await _notificationService.GetUserNotifications(userId, unreadOnly);
            return Ok(new ApiResponse<object>("Fetched user notifications successfully", notifications));
        }

        // GET: api/notification/unread-count/{userId}
        [HttpGet("unread-count/{userId}")]
        public async Task<IActionResult> GetUnreadCount(Guid userId)
        {
            var count = await _notificationService.GetUnreadCount(userId);
            return Ok(new ApiResponse<int>("Fetched unread count successfully", count));
        }

        // PUT: api/notification/mark-read/{notificationId}
        [HttpPut("mark-read/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            await _notificationService.MarkAsRead(notificationId);
            return Ok(new ApiResponse<string>("Marked as read successfully", null));
        }

        // PUT: api/notification/mark-all-read/{userId}
        [HttpPut("mark-all-read/{userId}")]
        public async Task<IActionResult> MarkAllAsRead(Guid userId)
        {
            await _notificationService.MarkAllAsRead(userId);
            return Ok(new ApiResponse<string>("All notifications marked as read", null));
        }

        // POST: api/notification/manual
        [HttpPost("manual")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendManualNotification([FromBody] ManualNotificationRequest request)
        {
            await _notificationService.SendNotification(request.UserId, request.Message, request.Type);
            return Ok(new ApiResponse<string>("Notification sent manually", null));
        }

        // POST: api/notification/order-status
        [HttpPost("order-status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> NotifyOrderStatusChange([FromBody] OrderStatusNotificationRequest request)
        {
            await _notificationService.NotifyOrderStatusChange(request.OrderId, request.OldStatus, request.NewStatus);
            return Ok(new ApiResponse<string>("Order status change notification sent", null));
        }

        /*// POST: api/notification/order-created/{orderId}
        [HttpPost("order-created/{orderId}")]
        public async Task<IActionResult> NotifyNewOrderCreated(Guid orderId)
        {
            await _notificationService.NotifyNewOrderCreated(orderId);
            return Ok(new ApiResponse<string>("New order creation notification sent", null));
        }

        // POST: api/notification/order-cancelled/{orderId}
        [HttpPost("order-cancelled/{orderId}")]
        public async Task<IActionResult> NotifyOrderCancellation(Guid orderId)
        {
            await _notificationService.NotifyOrderCancellation(orderId);
            return Ok(new ApiResponse<string>("Order cancellation notification sent", null));
        }

        // POST: api/notification/order-items-updated
        [HttpPost("order-items-updated")]
        public async Task<IActionResult> NotifyOrderItemsUpdate([FromBody] OrderItemsUpdatedRequest request)
        {
            await _notificationService.NotifyOrderItemsUpdate(request.OrderId, request.UpdatedItemIds);
            return Ok(new ApiResponse<string>("Order items update notification sent", null));
        }*/

        // POST: api/notification/transaction-failed?transactionId={guid}&userId={guid}
        [HttpPost("transaction-failed")]
        public async Task<IActionResult> NotifyTransactionFailed([FromQuery] Guid transactionId, [FromQuery] Guid userId)
        {
            try
            {
                await _notificationService.NotifyTransactionFailedByTransactionId(transactionId, userId);
                return Ok(new ApiResponse<string>("Transaction failed notifications sent", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error sending notification: {ex.Message}", null));
            }
        }
    }
}