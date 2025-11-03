using BusinessObject.DTOs.NotificationDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.NotificationRepositories;
using Services.NotificationServices;

namespace Services.Tests.Notification
{
    /// <summary>
    /// Unit tests for View Notifications functionality (Service Layer)
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬────────────────────────────────────────────┬──────────────────────────────────────┐
    /// │ Test ID │ Input                                      │ Expected Result                      │
    /// ├─────────┼────────────────────────────────────────────┼──────────────────────────────────────┤
    /// │ UTCID01 │ User has notifications (read + unread)    │ Return list of notifications         │
    /// │ UTCID02 │ User has no notifications                 │ Return empty list                    │
    /// └─────────┴────────────────────────────────────────────┴──────────────────────────────────────┘
    /// 
    /// Note: Service layer returns IEnumerable<NotificationResponse>
    ///       Message "You have no notifications." is a FRONTEND message (not from API)
    ///       API Message: "Fetched user notifications successfully" (from Controller)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~NotificationServiceTests"
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly Mock<INotificationRepository> _mockNotificationRepository;
        private readonly Mock<Repositories.RepositoryBase.IRepository<BusinessObject.Models.Order>> _mockOrderRepository;
        private readonly Mock<Microsoft.AspNetCore.SignalR.IHubContext<Hubs.NotificationHub>> _mockHubContext;
        private readonly NotificationService _notificationService;

        public NotificationServiceTests()
        {
            _mockNotificationRepository = new Mock<INotificationRepository>();
            _mockOrderRepository = new Mock<Repositories.RepositoryBase.IRepository<BusinessObject.Models.Order>>();
            _mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<Hubs.NotificationHub>>();

            // Pass null for DbContext as it's not used in GetUserNotifications method
            _notificationService = new NotificationService(
                _mockNotificationRepository.Object,
                _mockOrderRepository.Object,
                _mockHubContext.Object,
                null! // DbContext not needed for GetUserNotifications tests
            );
        }

        /// <summary>
        /// UTCID01: User has both read and unread notifications
        /// Expected: Return list of all notifications
        /// Backend Service: Returns IEnumerable<NotificationResponse>
        /// API Message (from Controller): "Fetched user notifications successfully"
        /// </summary>
        [Fact]
        public async Task UTCID01_GetUserNotifications_UserHasNotifications_ShouldReturnNotificationList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var notifications = new List<BusinessObject.Models.Notification>
            {
                new BusinessObject.Models.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = "Order created successfully",
                    Type = NotificationType.order,
                    IsRead = false, // Unread
                    CreatedAt = DateTime.UtcNow,
                    OrderId = orderId
                },
                new BusinessObject.Models.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = "Order status updated",
                    Type = NotificationType.order,
                    IsRead = true, // Read
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    OrderId = orderId
                },
                new BusinessObject.Models.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = "Payment completed",
                    Type = NotificationType.system,
                    IsRead = false, // Unread
                    CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                    OrderId = null
                }
            };

            _mockNotificationRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync(notifications);

            // Act
            var result = await _notificationService.GetUserNotifications(userId, unreadOnly: false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
            
            var notificationList = result.ToList();
            Assert.Contains(notificationList, n => n.Message == "Order created successfully" && !n.IsRead);
            Assert.Contains(notificationList, n => n.Message == "Order status updated" && n.IsRead);
            Assert.Contains(notificationList, n => n.Message == "Payment completed" && !n.IsRead && n.Type == NotificationType.system);

            _mockNotificationRepository.Verify(x => x.GetByUserIdAsync(userId), Times.Once);
            
            // API Controller returns: "Fetched user notifications successfully"
        }

        /// <summary>
        /// UTCID02: User has no notifications
        /// Expected: Return empty list
        /// Backend Service: Returns empty IEnumerable<NotificationResponse>
        /// API Message (from Controller): "Fetched user notifications successfully" (same message)
        /// Frontend Message: "You have no notifications." (NOT verified here - FE only)
        /// </summary>
        [Fact]
        public async Task UTCID02_GetUserNotifications_UserHasNoNotifications_ShouldReturnEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockNotificationRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync(new List<BusinessObject.Models.Notification>()); // Empty list

            // Act
            var result = await _notificationService.GetUserNotifications(userId, unreadOnly: false);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result); // Should return empty list, not null

            _mockNotificationRepository.Verify(x => x.GetByUserIdAsync(userId), Times.Once);

            // API Controller returns: "Fetched user notifications successfully" (same message for empty list)
            // Frontend displays: "You have no notifications." (FE message, not verified here)
        }

        /// <summary>
        /// Additional Test: Get only unread notifications
        /// </summary>
        [Fact]
        public async Task Additional_GetUserNotifications_UnreadOnly_ShouldReturnOnlyUnreadNotifications()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var unreadNotifications = new List<BusinessObject.Models.Notification>
            {
                new BusinessObject.Models.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = "Unread notification 1",
                    Type = NotificationType.order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                },
                new BusinessObject.Models.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = "Unread notification 2",
                    Type = NotificationType.message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            _mockNotificationRepository.Setup(x => x.GetUnreadByUserIdAsync(userId))
                .ReturnsAsync(unreadNotifications);

            // Act
            var result = await _notificationService.GetUserNotifications(userId, unreadOnly: true);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.All(result, n => Assert.False(n.IsRead)); // All should be unread

            _mockNotificationRepository.Verify(x => x.GetUnreadByUserIdAsync(userId), Times.Once);
            _mockNotificationRepository.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Verify NotificationResponse mapping
        /// </summary>
        [Fact]
        public async Task Additional_GetUserNotifications_ShouldMapToNotificationResponseCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var notificationId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var notifications = new List<BusinessObject.Models.Notification>
            {
                new BusinessObject.Models.Notification
                {
                    Id = notificationId,
                    UserId = userId,
                    Message = "Test notification",
                    Type = NotificationType.order,
                    IsRead = true,
                    CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0),
                    OrderId = orderId
                }
            };

            _mockNotificationRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync(notifications);

            // Act
            var result = await _notificationService.GetUserNotifications(userId);

            // Assert
            var notification = result.First();
            Assert.Equal(notificationId, notification.Id);
            Assert.Equal("Test notification", notification.Message);
            Assert.True(notification.IsRead);
            Assert.Equal(new DateTime(2024, 1, 1, 12, 0, 0), notification.CreatedAt);
            Assert.Equal(NotificationType.order, notification.Type);
            Assert.Equal(orderId, notification.OrderId);
        }
    }
}

