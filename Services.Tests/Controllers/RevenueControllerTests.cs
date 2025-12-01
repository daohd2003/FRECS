using BusinessObject.DTOs.RevenueDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Services.RevenueServices;
using ShareItAPI.Controllers;
using System.Collections.Generic;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    public class RevenueControllerTests
    {
        private readonly Mock<IRevenueService> _serviceMock;
        private readonly Mock<ILogger<RevenueController>> _loggerMock;
        private readonly RevenueController _controller;

        public RevenueControllerTests()
        {
            _serviceMock = new Mock<IRevenueService>();
            _loggerMock = new Mock<ILogger<RevenueController>>();
            _controller = new RevenueController(_serviceMock.Object, _loggerMock.Object);
        }

        private void SetupProvider(Guid? userId = null, string role = "provider")
        {
            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            claims.Add(new Claim(ClaimTypes.Role, role));

            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = context
            };
        }

        #region GetRevenueStats

        [Fact]
        public async Task UTCID01_GetRevenueStats_Provider_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var stats = new RevenueStatsDto { CurrentPeriodRevenue = 500 };
            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ReturnsAsync(stats);

            var result = await _controller.GetRevenueStats("month", null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<RevenueStatsDto>(okResult.Value);
            Assert.Equal(500, payload.CurrentPeriodRevenue);
            _serviceMock.Verify(s => s.GetRevenueStatsAsync(providerId, "month", null, null), Times.Once);
        }

        [Fact]
        public async Task UTCID02_GetRevenueStats_MissingUser_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.GetRevenueStats("month", null, null);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User not found", unauthorized.Value);
            _serviceMock.Verify(s => s.GetRevenueStatsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID03_GetRevenueStats_ArgumentException_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ThrowsAsync(new ArgumentException("Invalid range"));

            var result = await _controller.GetRevenueStats("month", null, null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid range", badRequest.Value);
        }

        [Fact]
        public async Task UTCID04_GetRevenueStats_ServiceError_ShouldReturn500()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ThrowsAsync(new Exception("db down"));

            var result = await _controller.GetRevenueStats("month", null, null);

            var serverError = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, serverError.StatusCode);
            Assert.Equal("Internal server error", serverError.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        #endregion

        #region GetPayoutSummary

        [Fact]
        public async Task UTCID05_GetPayoutSummary_ShouldReturnCurrentBalance()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var summary = new PayoutSummaryDto { CurrentBalance = 750 };
            _serviceMock.Setup(s => s.GetPayoutSummaryAsync(providerId)).ReturnsAsync(summary);

            var result = await _controller.GetPayoutSummary();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<PayoutSummaryDto>(okResult.Value);
            Assert.Equal(750, payload.CurrentBalance);
        }

        [Fact]
        public async Task UTCID06_GetPayoutSummary_MissingUser_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.GetPayoutSummary();

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User not found", unauthorized.Value);
        }

        [Fact]
        public async Task UTCID07_GetPayoutSummary_ServiceError_ShouldReturn500()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetPayoutSummaryAsync(providerId))
                .ThrowsAsync(new Exception("failure"));

            var result = await _controller.GetPayoutSummary();

            var serverError = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, serverError.StatusCode);
            Assert.Equal("Internal server error", serverError.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        #endregion

        #region GetPayoutHistory

        [Fact]
        public async Task UTCID08_GetPayoutHistory_ShouldReturnHistory()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var history = new List<PayoutHistoryDto>
            {
                new PayoutHistoryDto { Id = Guid.NewGuid(), Amount = 100, Status = "completed" }
            };
            _serviceMock.Setup(s => s.GetPayoutHistoryAsync(providerId, 1, 5))
                .ReturnsAsync(history);

            var result = await _controller.GetPayoutHistory(1, 5);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<List<PayoutHistoryDto>>(okResult.Value);
            Assert.Single(payload);
        }

        [Fact]
        public async Task UTCID09_GetPayoutHistory_NoUser_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.GetPayoutHistory();

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User not found", unauthorized.Value);
        }

        [Fact]
        public async Task UTCID10_GetPayoutHistory_ServiceError_ShouldReturn500()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetPayoutHistoryAsync(providerId, 1, 10))
                .ThrowsAsync(new Exception("boom"));

            var result = await _controller.GetPayoutHistory();

            var error = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Internal server error", error.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        #endregion

        #region RequestPayout

        [Fact]
        public async Task UTCID11_RequestPayout_ValidRequest_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.RequestPayoutAsync(providerId, 200))
                .ReturnsAsync(true);

            var result = await _controller.RequestPayout(new RevenueController.PayoutRequestDto { Amount = 200 });

            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            _serviceMock.Verify(s => s.RequestPayoutAsync(providerId, 200), Times.Once);
        }

        [Fact]
        public async Task UTCID12_RequestPayout_InvalidAmount_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var result = await _controller.RequestPayout(new RevenueController.PayoutRequestDto { Amount = 0 });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid amount", badRequest.Value);
            _serviceMock.Verify(s => s.RequestPayoutAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task UTCID13_RequestPayout_ServiceRejected_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.RequestPayoutAsync(providerId, 150))
                .ReturnsAsync(false);

            var result = await _controller.RequestPayout(new RevenueController.PayoutRequestDto { Amount = 150 });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Insufficient balance or other error", badRequest.Value);
        }

        [Fact]
        public async Task UTCID14_RequestPayout_NoUser_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.RequestPayout(new RevenueController.PayoutRequestDto { Amount = 100 });

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("User not found", unauthorized.Value);
        }

        [Fact]
        public async Task UTCID15_RequestPayout_ServiceError_ShouldReturn500()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.RequestPayoutAsync(providerId, 120))
                .ThrowsAsync(new Exception("error"));

            var result = await _controller.RequestPayout(new RevenueController.PayoutRequestDto { Amount = 120 });

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Internal server error", error.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        #endregion

        #region GetTopRevenue

        [Fact]
        public async Task UTCID16_GetTopRevenue_Provider_ShouldReturnOk()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var topRevenue = new List<TopRevenueItemDto>
            {
                new TopRevenueItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Product A",
                    ProductImageUrl = "https://example.com/image.jpg",
                    Revenue = 5000,
                    OrderCount = 5,
                    TransactionType = "rental"
                }
            };

            _serviceMock.Setup(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, 5))
                .ReturnsAsync(topRevenue);

            // Act
            var result = await _controller.GetTopRevenue("month", null, null, 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<List<TopRevenueItemDto>>(okResult.Value);
            Assert.Single(payload);
            Assert.Equal("Product A", payload[0].ProductName);
            Assert.Equal(5000, payload[0].Revenue);
            _serviceMock.Verify(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, 5), Times.Once);
        }

        [Fact]
        public async Task UTCID17_GetTopRevenue_MissingUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupProvider(null);

            // Act
            var result = await _controller.GetTopRevenue("month", null, null, 5);

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User not found", unauthorized.Value);
            _serviceMock.Verify(s => s.GetTopRevenueByProductAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UTCID18_GetTopRevenue_ArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, 5))
                .ThrowsAsync(new ArgumentException("Invalid date range"));

            // Act
            var result = await _controller.GetTopRevenue("month", null, null, 5);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid date range", badRequest.Value);
        }

        [Fact]
        public async Task UTCID19_GetTopRevenue_ServiceError_ShouldReturn500()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, 5))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetTopRevenue("month", null, null, 5);

            // Assert
            var serverError = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, serverError.StatusCode);
            Assert.Equal("Internal server error", serverError.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task UTCID20_GetTopRevenue_WithCustomDates_ShouldPassDatesToService()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var startDate = new DateTime(2024, 2, 1);
            var endDate = new DateTime(2024, 2, 28);

            var topRevenue = new List<TopRevenueItemDto>();

            _serviceMock.Setup(s => s.GetTopRevenueByProductAsync(providerId, "month", startDate, endDate, 5))
                .ReturnsAsync(topRevenue);

            // Act
            var result = await _controller.GetTopRevenue("month", startDate, endDate, 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            _serviceMock.Verify(s => s.GetTopRevenueByProductAsync(providerId, "month", startDate, endDate, 5), Times.Once);
        }

        [Fact]
        public async Task UTCID21_GetTopRevenue_WithCustomLimit_ShouldPassLimitToService()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var limit = 10;

            var topRevenue = new List<TopRevenueItemDto>();

            _serviceMock.Setup(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, limit))
                .ReturnsAsync(topRevenue);

            // Act
            var result = await _controller.GetTopRevenue("month", null, null, limit);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            _serviceMock.Verify(s => s.GetTopRevenueByProductAsync(providerId, "month", null, null, limit), Times.Once);
        }

        #endregion

        #region GetTopCustomers

        [Fact]
        public async Task UTCID22_GetTopCustomers_Provider_ShouldReturnOk()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var topCustomers = new List<TopCustomerDto>
            {
                new TopCustomerDto
                {
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "John Doe",
                    CustomerEmail = "john@example.com",
                    CustomerAvatarUrl = "https://example.com/avatar.jpg",
                    TotalSpent = 10000,
                    OrderCount = 8
                }
            };

            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", null, null, 5))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<List<TopCustomerDto>>(okResult.Value);
            Assert.Single(payload);
            Assert.Equal("John Doe", payload[0].CustomerName);
            Assert.Equal(10000, payload[0].TotalSpent);
            Assert.Equal(8, payload[0].OrderCount);
            _serviceMock.Verify(s => s.GetTopCustomersAsync(providerId, "month", null, null, 5), Times.Once);
        }

        [Fact]
        public async Task UTCID23_GetTopCustomers_MissingUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupProvider(null);

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, 5);

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User not found", unauthorized.Value);
            _serviceMock.Verify(s => s.GetTopCustomersAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UTCID24_GetTopCustomers_ArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", null, null, 5))
                .ThrowsAsync(new ArgumentException("Invalid date range"));

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, 5);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid date range", badRequest.Value);
        }

        [Fact]
        public async Task UTCID25_GetTopCustomers_ServiceError_ShouldReturn500()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", null, null, 5))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, 5);

            // Assert
            var serverError = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, serverError.StatusCode);
            Assert.Equal("Internal server error", serverError.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task UTCID26_GetTopCustomers_WithCustomDates_ShouldPassDatesToService()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var startDate = new DateTime(2024, 2, 1);
            var endDate = new DateTime(2024, 2, 28);

            var topCustomers = new List<TopCustomerDto>();

            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", startDate, endDate, 5))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _controller.GetTopCustomers("month", startDate, endDate, 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            _serviceMock.Verify(s => s.GetTopCustomersAsync(providerId, "month", startDate, endDate, 5), Times.Once);
        }

        [Fact]
        public async Task UTCID27_GetTopCustomers_WithCustomLimit_ShouldPassLimitToService()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var limit = 10;

            var topCustomers = new List<TopCustomerDto>();

            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", null, null, limit))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, limit);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            _serviceMock.Verify(s => s.GetTopCustomersAsync(providerId, "month", null, null, limit), Times.Once);
        }

        [Fact]
        public async Task UTCID28_GetTopCustomers_EmptyResult_ShouldReturnEmptyList()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var topCustomers = new List<TopCustomerDto>();

            _serviceMock.Setup(s => s.GetTopCustomersAsync(providerId, "month", null, null, 5))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _controller.GetTopCustomers("month", null, null, 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<List<TopCustomerDto>>(okResult.Value);
            Assert.Empty(payload);
        }

        #endregion

        #region GetProviderRevenueStats (Admin)

        [Fact]
        public async Task UTCID48_GetProviderRevenueStats_Admin_ShouldReturnOk()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            SetupProvider(adminId, "admin");

            var stats = new RevenueStatsDto { CurrentPeriodRevenue = 1000 };
            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderRevenueStats(providerId, "month", null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<RevenueStatsDto>(okResult.Value);
            Assert.Equal(1000, payload.CurrentPeriodRevenue);
            _serviceMock.Verify(s => s.GetRevenueStatsAsync(providerId, "month", null, null), Times.Once);
        }

        [Fact]
        public async Task UTCID49_GetProviderRevenueStats_ArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            SetupProvider(adminId, "admin");

            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ThrowsAsync(new ArgumentException("Invalid date range"));

            // Act
            var result = await _controller.GetProviderRevenueStats(providerId, "month", null, null);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid date range", badRequest.Value);
        }

        [Fact]
        public async Task UTCID50_GetProviderRevenueStats_ServiceError_ShouldReturn500()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            SetupProvider(adminId, "admin");

            _serviceMock.Setup(s => s.GetRevenueStatsAsync(providerId, "month", null, null))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetProviderRevenueStats(providerId, "month", null, null);

            // Assert
            var serverError = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, serverError.StatusCode);
            Assert.Equal("Internal server error", serverError.Value);
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        #endregion
    }

    internal static class LoggerMockExtensions
    {
        public static void VerifyLog(this Mock<ILogger<RevenueController>> loggerMock, LogLevel level)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}

