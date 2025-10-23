using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.NotificationDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.NotificationServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for Notification Controller (API Layer)
    /// Verifies API messages and HTTP status codes
    /// 
    /// Test Coverage:
    /// - Get User Notifications (GET /api/notification/user/{userId})
    /// 
    /// Note: Controller returns ApiResponse<object> with message
    ///       Frontend message "You have no notifications." is NOT tested (FE only)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~NotificationControllerTests"
    /// </summary>
    public class NotificationControllerTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly NotificationController _controller;

        public NotificationControllerTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _controller = new NotificationController(_mockNotificationService.Object);
        }

        #region GetUserNotifications Tests

        /// <summary>
        /// UTCID01: User has both read and unread notifications
        /// Expected: 200 OK with notification list
        /// API Message: "Fetched user notifications successfully"
        /// Frontend: Displays all notifications (read and unread)
        /// </summary>
        [Fact]
        public async Task UTCID01_GetUserNotifications_UserHasNotifications_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var notifications = new List<NotificationResponse>
            {
                new NotificationResponse
                {
                    Id = Guid.NewGuid(),
                    Message = "Order created successfully",
                    Type = NotificationType.order,
                    IsRead = false, // Unread
                    CreatedAt = DateTime.UtcNow,
                    OrderId = orderId
                },
                new NotificationResponse
                {
                    Id = Guid.NewGuid(),
                    Message = "Order status updated",
                    Type = NotificationType.order,
                    IsRead = true, // Read
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    OrderId = orderId
                },
                new NotificationResponse
                {
                    Id = Guid.NewGuid(),
                    Message = "Payment completed",
                    Type = NotificationType.system,
                    IsRead = false, // Unread
                    CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                    OrderId = null
                }
            };

            _mockNotificationService.Setup(x => x.GetUserNotifications(userId, false))
                .ReturnsAsync(notifications);

            // Act
            var result = await _controller.GetUserNotifications(userId, unreadOnly: false);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Fetched user notifications successfully", apiResponse.Message); // Verify API message

            // Verify data contains notifications
            var returnedNotifications = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(apiResponse.Data);
            Assert.Equal(3, returnedNotifications.Count());
            
            _mockNotificationService.Verify(x => x.GetUserNotifications(userId, false), Times.Once);

            // Frontend displays all notifications (read and unread)
        }

        /// <summary>
        /// UTCID02: User has no notifications
        /// Expected: 200 OK with empty list
        /// API Message: "Fetched user notifications successfully" (same message)
        /// Frontend Message: "You have no notifications." (FE only - NOT verified here)
        /// </summary>
        [Fact]
        public async Task UTCID02_GetUserNotifications_UserHasNoNotifications_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var emptyNotifications = Enumerable.Empty<NotificationResponse>();

            _mockNotificationService.Setup(x => x.GetUserNotifications(userId, false))
                .ReturnsAsync(emptyNotifications);

            // Act
            var result = await _controller.GetUserNotifications(userId, unreadOnly: false);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Fetched user notifications successfully", apiResponse.Message); // Verify API message (same for empty)

            // Verify data is empty
            var returnedNotifications = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(apiResponse.Data);
            Assert.Empty(returnedNotifications);

            _mockNotificationService.Verify(x => x.GetUserNotifications(userId, false), Times.Once);

            // API returns same success message even for empty list
            // Frontend displays: "You have no notifications." (FE message, not from API)
        }

        /// <summary>
        /// Additional Test: Get only unread notifications
        /// </summary>
        [Fact]
        public async Task Additional_GetUserNotifications_UnreadOnly_ShouldReturnOnlyUnreadNotifications()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var unreadNotifications = new List<NotificationResponse>
            {
                new NotificationResponse
                {
                    Id = Guid.NewGuid(),
                    Message = "Unread notification",
                    Type = NotificationType.order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockNotificationService.Setup(x => x.GetUserNotifications(userId, true))
                .ReturnsAsync(unreadNotifications);

            // Act
            var result = await _controller.GetUserNotifications(userId, unreadOnly: true);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Fetched user notifications successfully", apiResponse.Message);

            var returnedNotifications = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(apiResponse.Data);
            Assert.Single(returnedNotifications);
            Assert.All(returnedNotifications, n => Assert.False(n.IsRead));

            _mockNotificationService.Verify(x => x.GetUserNotifications(userId, true), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify notification details are returned correctly
        /// </summary>
        [Fact]
        public async Task Additional_GetUserNotifications_ShouldReturnNotificationDetails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var notificationId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var notifications = new List<NotificationResponse>
            {
                new NotificationResponse
                {
                    Id = notificationId,
                    Message = "Test notification message",
                    Type = NotificationType.system,
                    IsRead = true,
                    CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0),
                    OrderId = orderId
                }
            };

            _mockNotificationService.Setup(x => x.GetUserNotifications(userId, false))
                .ReturnsAsync(notifications);

            // Act
            var result = await _controller.GetUserNotifications(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);

            var returnedNotifications = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(apiResponse.Data);
            var notification = returnedNotifications.First();

            Assert.Equal(notificationId, notification.Id);
            Assert.Equal("Test notification message", notification.Message);
            Assert.Equal(NotificationType.system, notification.Type);
            Assert.True(notification.IsRead);
            Assert.Equal(orderId, notification.OrderId);
        }

        #endregion
    }
}

