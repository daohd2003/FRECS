using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.OrderServices;
using ShareItAPI.Controllers;
using System.Security.Claims;
using Xunit;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for OrderController - Provider-specific endpoints
    /// Tests cover authorization and provider order management operations
    /// </summary>
    public class ProviderOrderControllerTests
    {
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly OrderController _controller;
        private readonly Guid _providerId;
        private readonly Guid _customerId;

        public ProviderOrderControllerTests()
        {
            _mockOrderService = new Mock<IOrderService>();
            _controller = new OrderController(_mockOrderService.Object);
            _providerId = Guid.NewGuid();
            _customerId = Guid.NewGuid();

            // Setup controller context with provider claims
            SetupControllerContext(_providerId.ToString(), "provider");
        }

        private void SetupControllerContext(string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region Get Orders By Provider Tests

        [Fact]
        public async Task GetOrdersByProvider_ProviderAccessingOwnOrders_ShouldReturnOk()
        {
            // Arrange
            var orders = new List<OrderDto>
            {
                new OrderDto { Id = Guid.NewGuid(), ProviderId = _providerId, TotalAmount = 1000 }
            };

            _mockOrderService.Setup(s => s.GetOrdersByProviderAsync(_providerId))
                .ReturnsAsync(orders);

            // Act
            var result = await _controller.GetOrdersByProvider(_providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Orders by provider", response.Message);
            _mockOrderService.Verify(s => s.GetOrdersByProviderAsync(_providerId), Times.Once);
        }

        [Fact]
        public async Task GetOrdersByProvider_ProviderAccessingOtherProviderOrders_ShouldReturnForbid()
        {
            // Arrange
            var otherProviderId = Guid.NewGuid();

            // Act
            var result = await _controller.GetOrdersByProvider(otherProviderId);

            // Assert
            Assert.IsType<ForbidResult>(result);
            _mockOrderService.Verify(s => s.GetOrdersByProviderAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetOrdersByProvider_AdminAccessingAnyProviderOrders_ShouldReturnOk()
        {
            // Arrange
            SetupControllerContext(Guid.NewGuid().ToString(), "admin");
            var anyProviderId = Guid.NewGuid();
            var orders = new List<OrderDto>
            {
                new OrderDto { Id = Guid.NewGuid(), ProviderId = anyProviderId }
            };

            _mockOrderService.Setup(s => s.GetOrdersByProviderAsync(anyProviderId))
                .ReturnsAsync(orders);

            // Act
            var result = await _controller.GetOrdersByProvider(anyProviderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockOrderService.Verify(s => s.GetOrdersByProviderAsync(anyProviderId), Times.Once);
        }

        [Fact]
        public async Task GetOrdersByProvider_UnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            // Act
            var result = await _controller.GetOrdersByProvider(_providerId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.Equal("User not authenticated.", response.Message);
        }

        #endregion

        #region Get Provider Orders For List Display Tests

        [Fact]
        public async Task GetProviderOrdersForListDisplay_ValidProvider_ShouldReturnOk()
        {
            // Arrange
            var orders = new List<OrderListDto>
            {
                new OrderListDto
                {
                    Id = Guid.NewGuid(),
                    Status = OrderStatus.pending,
                    TotalAmount = 1000,
                    CustomerName = "Test Customer"
                }
            };

            _mockOrderService.Setup(s => s.GetProviderOrdersForListDisplayAsync(_providerId))
                .ReturnsAsync(orders);

            // Act
            var result = await _controller.GetProviderOrdersForListDisplay(_providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Contains("list display retrieved", response.Message);
            _mockOrderService.Verify(s => s.GetProviderOrdersForListDisplayAsync(_providerId), Times.Once);
        }

        [Fact]
        public async Task GetProviderOrdersForListDisplay_ProviderAccessingOtherProvider_ShouldReturnForbid()
        {
            // Arrange
            var otherProviderId = Guid.NewGuid();

            // Act
            var result = await _controller.GetProviderOrdersForListDisplay(otherProviderId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        #endregion

        #region Get Order Details For Provider Tests

        [Fact]
        public async Task GetOrderDetailsForProvider_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var orderDetails = new OrderDetailsDto
            {
                Id = orderId,
                ProviderId = _providerId,
                Status = OrderStatus.approved,
                TotalAmount = 1500
            };

            _mockOrderService.Setup(s => s.GetOrderDetailsForProviderAsync(orderId))
                .ReturnsAsync(orderDetails);

            // Act
            var result = await _controller.GetOrderDetailsForProvider(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<OrderDetailsDto>>(okResult.Value);
            Assert.Equal("Provider order details retrieved successfully.", response.Message);
            Assert.Equal(orderId, response.Data.Id);
        }

        [Fact]
        public async Task GetOrderDetailsForProvider_OrderNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.GetOrderDetailsForProviderAsync(orderId))
                .ReturnsAsync((OrderDetailsDto)null);

            // Act
            var result = await _controller.GetOrderDetailsForProvider(orderId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.Contains("not found", response.Message);
        }

        [Fact]
        public async Task GetOrderDetailsForProvider_ProviderAccessingOtherProviderOrder_ShouldReturnForbid()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var otherProviderId = Guid.NewGuid();
            var orderDetails = new OrderDetailsDto
            {
                Id = orderId,
                ProviderId = otherProviderId,
                Status = OrderStatus.approved
            };

            _mockOrderService.Setup(s => s.GetOrderDetailsForProviderAsync(orderId))
                .ReturnsAsync(orderDetails);

            // Act
            var result = await _controller.GetOrderDetailsForProvider(orderId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetOrderDetailsForProvider_AdminAccessingAnyOrder_ShouldReturnOk()
        {
            // Arrange
            SetupControllerContext(Guid.NewGuid().ToString(), "admin");
            var orderId = Guid.NewGuid();
            var anyProviderId = Guid.NewGuid();
            var orderDetails = new OrderDetailsDto
            {
                Id = orderId,
                ProviderId = anyProviderId,
                Status = OrderStatus.approved
            };

            _mockOrderService.Setup(s => s.GetOrderDetailsForProviderAsync(orderId))
                .ReturnsAsync(orderDetails);

            // Act
            var result = await _controller.GetOrderDetailsForProvider(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockOrderService.Verify(s => s.GetOrderDetailsForProviderAsync(orderId), Times.Once);
        }

        #endregion

        #region Get Provider Dashboard Stats Tests

        [Fact]
        public async Task GetProviderDashboardStats_ValidProvider_ShouldReturnOk()
        {
            // Arrange
            var stats = new DashboardStatsDTO
            {
                PendingCount = 5,
                ApprovedCount = 10,
                InUseCount = 3,
                ReturnedCount = 20,
                CancelledCount = 2
            };

            _mockOrderService.Setup(s => s.GetProviderDashboardStatsAsync(_providerId))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderDashboardStats(_providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Provider dashboard statistics", response.Message);
            _mockOrderService.Verify(s => s.GetProviderDashboardStatsAsync(_providerId), Times.Once);
        }

        [Fact]
        public async Task GetProviderDashboardStats_ProviderAccessingOtherProviderStats_ShouldReturnForbid()
        {
            // Arrange
            var otherProviderId = Guid.NewGuid();

            // Act
            var result = await _controller.GetProviderDashboardStats(otherProviderId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetProviderDashboardStats_AdminAccessingAnyProviderStats_ShouldReturnOk()
        {
            // Arrange
            SetupControllerContext(Guid.NewGuid().ToString(), "admin");
            var anyProviderId = Guid.NewGuid();
            var stats = new DashboardStatsDTO
            {
                PendingCount = 5,
                ApprovedCount = 10
            };

            _mockOrderService.Setup(s => s.GetProviderDashboardStatsAsync(anyProviderId))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderDashboardStats(anyProviderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockOrderService.Verify(s => s.GetProviderDashboardStatsAsync(anyProviderId), Times.Once);
        }

        #endregion

        #region Mark As Shipping Tests

        [Fact]
        public async Task MarkAsShipping_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsShipingAsync(orderId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.MarkAsShipping(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Order marked as shipping", response.Message);
            _mockOrderService.Verify(s => s.MarkAsShipingAsync(orderId), Times.Once);
        }

        [Fact]
        public async Task MarkAsShipping_ServiceThrowsException_ShouldPropagate()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsShipingAsync(orderId))
                .ThrowsAsync(new Exception("Order not found"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _controller.MarkAsShipping(orderId));
        }

        #endregion

        #region Change Order Status Tests

        [Fact]
        public async Task ChangeOrderStatus_ValidStatusChange_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var newStatus = OrderStatus.in_transit;

            _mockOrderService.Setup(s => s.ChangeOrderStatus(orderId, newStatus))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ChangeOrderStatus(orderId, newStatus);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains(newStatus.ToString(), response.Message);
            _mockOrderService.Verify(s => s.ChangeOrderStatus(orderId, newStatus), Times.Once);
        }

        #endregion

        #region Mark As Returned Tests

        [Fact]
        public async Task MarkAsReturned_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturnedAsync(orderId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.MarkAsReturned(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Order marked as returned", response.Message);
        }

        #endregion

        #region Confirm Delivery Tests

        [Fact]
        public async Task ConfirmDelivery_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.ConfirmDeliveryAsync(orderId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ConfirmDelivery(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Order delivery confirmed successfully", response.Message);
        }

        [Fact]
        public async Task ConfirmDelivery_ServiceThrowsException_ShouldReturnBadRequest()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.ConfirmDeliveryAsync(orderId))
                .ThrowsAsync(new Exception("Invalid order status"));

            // Act
            var result = await _controller.ConfirmDelivery(orderId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("Invalid order status", response.Message);
        }

        #endregion

        #region Mark As Returning Tests

        [Fact]
        public async Task MarkAsReturning_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturningAsync(orderId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.MarkAsReturning(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Order marked as returning", response.Message);
            _mockOrderService.Verify(s => s.MarkAsReturningAsync(orderId), Times.Once);
        }

        [Fact]
        public async Task MarkAsReturning_InvalidStatus_ShouldReturnBadRequest()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturningAsync(orderId))
                .ThrowsAsync(new Exception("Order must be in use status to mark as returning"));

            // Act
            var result = await _controller.MarkAsReturning(orderId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("must be in use status", response.Message);
        }

        [Fact]
        public async Task MarkAsReturning_OrderNotFound_ShouldReturnBadRequest()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturningAsync(orderId))
                .ThrowsAsync(new Exception("Order not found"));

            // Act
            var result = await _controller.MarkAsReturning(orderId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("Order not found", response.Message);
        }

        #endregion

        #region Mark As Returned With Issue Tests

        [Fact]
        public async Task MarkAsReturnedWithIssue_ValidOrder_ShouldReturnOk()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturnedWithIssueAsync(orderId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.MarkAsReturnedWithIssue(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("returned_with_issue", response.Message);
            _mockOrderService.Verify(s => s.MarkAsReturnedWithIssueAsync(orderId), Times.Once);
        }

        [Fact]
        public async Task MarkAsReturnedWithIssue_ServiceThrowsException_ShouldPropagate()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _mockOrderService.Setup(s => s.MarkAsReturnedWithIssueAsync(orderId))
                .ThrowsAsync(new InvalidOperationException("Invalid status transition"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.MarkAsReturnedWithIssue(orderId));
        }

        #endregion
    }
}

