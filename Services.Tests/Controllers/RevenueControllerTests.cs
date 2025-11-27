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

