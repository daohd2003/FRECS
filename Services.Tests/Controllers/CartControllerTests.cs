using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.CartServices;
using Services.OrderServices;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for CartController.Checkout method (API Layer)
    /// Tests the Rent or Purchase clothes functionality
    /// 
    /// Verifies API responses and HTTP status codes
    /// 
    /// Validation messages from CheckoutRequestDto:
    ///  - "Rental start date is required." ([Required] on RentalStart)
    ///  - "Rental end date is required." ([Required] on RentalEnd)
    /// 
    /// API messages:
    ///  - Success (201): "Orders created successfully from cart."
    ///  - Policy not agreed (400): "You must read and agree to the Rental and Sales Policy to proceed with payment."
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~CartControllerTests"
    /// </summary>
    public class CartControllerTests
    {
        private readonly Mock<ICartService> _mockCartService;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly CartController _controller;

        public CartControllerTests()
        {
            _mockCartService = new Mock<ICartService>();
            _mockOrderService = new Mock<IOrderService>();
            _controller = new CartController(_mockCartService.Object, _mockOrderService.Object);
        }

        private void SetupUserContext(Guid userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region Checkout Tests

        /// <summary>
        /// UTCID01: Valid RentalStart, valid RentalEnd, HasAgreedToPolicies = TRUE
        /// Expected: 201 Created with message "Orders created successfully from cart."
        /// API message: "Orders created successfully from cart."
        /// </summary>
        [Fact]
        public async Task UTCID01_Checkout_ValidDatesAndAgreed_ShouldReturn201Created()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true,
                CustomerFullName = "Test Customer",
                CustomerEmail = "test@example.com",
                CustomerPhoneNumber = "0123456789",
                DeliveryAddress = "123 Test Street"
            };

            var orderDtos = new List<OrderDto>
            {
                new OrderDto
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    ProviderId = Guid.NewGuid()
                }
            };

            _mockOrderService.Setup(x => x.CreateOrderFromCartAsync(customerId, checkoutDto))
                .ReturnsAsync(orderDtos);

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<IEnumerable<OrderDto>>>(objectResult.Value);
            Assert.Equal("Orders created successfully from cart.", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            _mockOrderService.Verify(x => x.CreateOrderFromCartAsync(customerId, checkoutDto), Times.Once);
        }

        /// <summary>
        /// UTCID04: Valid RentalStart, valid RentalEnd, HasAgreedToPolicies = FALSE
        /// Expected: 400 Bad Request
        /// API message: "You must read and agree to the Rental and Sales Policy to proceed with payment."
        /// </summary>
        [Fact]
        public async Task UTCID04_Checkout_NotAgreedToPolicy_ShouldReturn400BadRequest()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = false, // Not agreed to policies
                CustomerFullName = "Test Customer"
            };

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("You must read and agree to the Rental and Sales Policy to proceed with payment.", apiResponse.Message);

            // Verify service was never called
            _mockOrderService.Verify(x => x.CreateOrderFromCartAsync(It.IsAny<Guid>(), It.IsAny<CheckoutRequestDto>()), Times.Never);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Checkout without user context should return 401 Unauthorized
        /// </summary>
        [Fact]
        public async Task Additional_Checkout_NoUserContext_ShouldReturn401Unauthorized()
        {
            // Arrange
            // Setup empty user context (no claims)
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true
            };

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized access.", apiResponse.Message);

            _mockOrderService.Verify(x => x.CreateOrderFromCartAsync(It.IsAny<Guid>(), It.IsAny<CheckoutRequestDto>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Service throws ArgumentException (empty cart) should return 404 Not Found
        /// </summary>
        [Fact]
        public async Task Additional_Checkout_EmptyCart_ShouldReturn404NotFound()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true
            };

            _mockOrderService.Setup(x => x.CreateOrderFromCartAsync(customerId, checkoutDto))
                .ThrowsAsync(new ArgumentException("Cart is empty or not found."));

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.Equal("Cart is empty or not found.", apiResponse.Message);
        }

        /// <summary>
        /// Additional Test: Service throws InvalidOperationException should return 409 Conflict
        /// </summary>
        [Fact]
        public async Task Additional_Checkout_ProductUnavailable_ShouldReturn409Conflict()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true
            };

            _mockOrderService.Setup(x => x.CreateOrderFromCartAsync(customerId, checkoutDto))
                .ThrowsAsync(new InvalidOperationException("Product 'Test Product' is unavailable."));

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflictResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(conflictResult.Value);
            Assert.Contains("unavailable", apiResponse.Message);
        }

        /// <summary>
        /// Additional Test: Generic exception should return 500 Internal Server Error
        /// </summary>
        [Fact]
        public async Task Additional_Checkout_GenericException_ShouldReturn500()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true
            };

            _mockOrderService.Setup(x => x.CreateOrderFromCartAsync(customerId, checkoutDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
            Assert.Contains("unexpected error", apiResponse.Message);
        }

        /// <summary>
        /// Additional Test: Verify multiple orders can be created (multiple providers)
        /// </summary>
        [Fact]
        public async Task Additional_Checkout_MultipleProviders_ShouldReturnMultipleOrders()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var checkoutDto = new CheckoutRequestDto
            {
                RentalStart = DateTime.UtcNow.AddDays(1),
                RentalEnd = DateTime.UtcNow.AddDays(8),
                HasAgreedToPolicies = true
            };

            var orderDtos = new List<OrderDto>
            {
                new OrderDto { Id = Guid.NewGuid(), ProviderId = Guid.NewGuid() },
                new OrderDto { Id = Guid.NewGuid(), ProviderId = Guid.NewGuid() }
            };

            _mockOrderService.Setup(x => x.CreateOrderFromCartAsync(customerId, checkoutDto))
                .ReturnsAsync(orderDtos);

            // Act
            var result = await _controller.Checkout(checkoutDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<IEnumerable<OrderDto>>>(objectResult.Value);
            Assert.Equal(2, apiResponse.Data.Count());
        }

        #endregion
    }
}

