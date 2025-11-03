using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CustomerDashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.CustomerDashboardServices;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for CustomerDashboardController (API Layer)
    /// Tests track spending functionality at controller level
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~CustomerDashboardControllerTests"
    /// </summary>
    public class CustomerDashboardControllerTests
    {
        private readonly Mock<ICustomerDashboardService> _mockService;
        private readonly CustomerDashboardController _controller;
        private readonly Guid _testCustomerId;

        public CustomerDashboardControllerTests()
        {
            _mockService = new Mock<ICustomerDashboardService>();
            _controller = new CustomerDashboardController(_mockService.Object);
            _testCustomerId = Guid.NewGuid();

            // Setup user claims for authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _testCustomerId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        /// <summary>
        /// Get spending stats - Valid request
        /// Expected: 200 OK with spending statistics
        /// </summary>
        [Fact]
        public async Task GetSpendingStats_ValidRequest_ShouldReturn200WithStats()
        {
            // Arrange
            var period = "week";
            var stats = new CustomerSpendingStatsDto
            {
                ThisPeriodSpending = 500,
                OrdersCount = 5,
                SpendingChangePercentage = 25
            };

            _mockService.Setup(x => x.GetSpendingStatsAsync(_testCustomerId, period))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetSpendingStats(period);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<CustomerSpendingStatsDto>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(500, apiResponse.Data.ThisPeriodSpending);
            Assert.Equal(5, apiResponse.Data.OrdersCount);

            _mockService.Verify(x => x.GetSpendingStatsAsync(_testCustomerId, period), Times.Once);
        }

        /// <summary>
        /// Get spending trend - Valid request
        /// Expected: 200 OK with trend data
        /// </summary>
        [Fact]
        public async Task GetSpendingTrend_ValidRequest_ShouldReturn200WithTrend()
        {
            // Arrange
            var period = "week";
            var trendData = new List<SpendingTrendDto>
            {
                new SpendingTrendDto { Date = "Mon", Amount = 100 },
                new SpendingTrendDto { Date = "Tue", Amount = 150 }
            };

            _mockService.Setup(x => x.GetSpendingTrendAsync(_testCustomerId, period))
                .ReturnsAsync(trendData);

            // Act
            var result = await _controller.GetSpendingTrend(period);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<List<SpendingTrendDto>>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(2, apiResponse.Data.Count);

            _mockService.Verify(x => x.GetSpendingTrendAsync(_testCustomerId, period), Times.Once);
        }

        /// <summary>
        /// Get spending by category - Valid request
        /// Expected: 200 OK with category breakdown
        /// </summary>
        [Fact]
        public async Task GetSpendingByCategory_ValidRequest_ShouldReturn200WithCategories()
        {
            // Arrange
            var period = "month";
            var categoryData = new List<SpendingByCategoryDto>
            {
                new SpendingByCategoryDto { CategoryName = "Clothing", TotalSpending = 300, OrderCount = 5 },
                new SpendingByCategoryDto { CategoryName = "Accessories", TotalSpending = 200, OrderCount = 3 }
            };

            _mockService.Setup(x => x.GetSpendingByCategoryAsync(_testCustomerId, period))
                .ReturnsAsync(categoryData);

            // Act
            var result = await _controller.GetSpendingByCategory(period);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<SpendingByCategoryDto>>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(2, apiResponse.Data.Count);
        }

        /// <summary>
        /// Unauthorized access - No user claims
        /// Expected: 401 Unauthorized
        /// </summary>
        [Fact]
        public async Task GetSpendingStats_NoUserClaims_ShouldReturn401()
        {
            // Arrange
            var controller = new CustomerDashboardController(_mockService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await controller.GetSpendingStats("week");

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
        }
    }
}

