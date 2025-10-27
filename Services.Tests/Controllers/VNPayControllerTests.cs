using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.VNPay;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Services.CartServices;
using Services.OrderServices;
using Services.Payments.VNPay;
using Services.Transactions;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for VNPayController.CreatePaymentUrl method (API Layer)
    /// Tests the Make payments functionality
    /// 
    /// Verifies API responses and HTTP status codes
    /// 
    /// API messages:
    ///  - Success (200): "Payment URL created successfully"
    ///  - Empty order IDs (400): "Order IDs are required."
    ///  - No valid orders (400): "No valid orders to pay for."
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~VNPayControllerTests"
    /// </summary>
    public class VNPayControllerTests
    {
        private readonly Mock<IVnpay> _mockVnpay;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<VNPayController>> _mockLogger;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<ITransactionService> _mockTransactionService;
        private readonly Mock<ICartService> _mockCartService;
        private readonly VNPayController _controller;

        public VNPayControllerTests()
        {
            _mockVnpay = new Mock<IVnpay>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<VNPayController>>();
            _mockOrderService = new Mock<IOrderService>();
            _mockTransactionService = new Mock<ITransactionService>();
            _mockCartService = new Mock<ICartService>();

            // Setup configuration values
            _mockConfiguration.Setup(x => x["Vnpay:TmnCode"]).Returns("TESTCODE");
            _mockConfiguration.Setup(x => x["Vnpay:HashSecret"]).Returns("TESTSECRET");
            _mockConfiguration.Setup(x => x["Vnpay:BaseUrl"]).Returns("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html");

            // DbContext doesn't have parameterless constructor, pass null as it's not directly used in CreatePaymentUrl
            _controller = new VNPayController(
                _mockVnpay.Object,
                _mockConfiguration.Object,
                _mockEnvironment.Object,
                _mockLogger.Object,
                _mockTransactionService.Object,
                _mockOrderService.Object,
                _mockCartService.Object,
                null! // ShareItDbContext not used in CreatePaymentUrl tests
            );
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

        #region CreatePaymentUrl Tests

        /// <summary>
        /// UTCID01: Valid pending order IDs for the user
        /// Expected: 200 OK with payment URL
        /// API message: "Payment URL created successfully"
        /// </summary>
        [Fact]
        public async Task UTCID01_CreatePaymentUrl_ValidPendingOrders_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid> { orderId },
                Note = "Payment for order"
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.pending,
                TotalAmount = 100000
            };

            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockTransactionService.Setup(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()))
                .ReturnsAsync(It.IsAny<BusinessObject.Models.Transaction>());

            _mockVnpay.Setup(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()))
                .Returns("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=...");

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var okResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
            Assert.Equal(200, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(objectResult.Value);
            Assert.Equal("Payment URL created successfully", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);
            Assert.Contains("vnpayment.vn", apiResponse.Data);

            _mockTransactionService.Verify(x => x.SaveTransactionAsync(It.Is<BusinessObject.Models.Transaction>(
                t => t.CustomerId == customerId &&
                     t.Amount == 100000 &&
                     t.Status == BusinessObject.Enums.TransactionStatus.initiated
            )), Times.Once);

            _mockVnpay.Verify(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()), Times.Once);
        }

        /// <summary>
        /// UTCID02: Empty order IDs list
        /// Expected: 400 Bad Request
        /// API message: "Order IDs are required."
        /// </summary>
        [Fact]
        public async Task UTCID02_CreatePaymentUrl_EmptyOrderIds_ShouldReturn400BadRequest()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid>(), // Empty list
                Note = "Payment note"
            };

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var badRequestResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
            Assert.Equal(400, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(objectResult.Value);
            Assert.Equal("Order IDs are required.", apiResponse.Message);
            Assert.Null(apiResponse.Data);

            _mockOrderService.Verify(x => x.GetOrderEntityByIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockTransactionService.Verify(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()), Times.Never);
            _mockVnpay.Verify(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()), Times.Never);
        }

        /// <summary>
        /// UTCID03: Order IDs that are invalid, not pending, or belong to another user
        /// Expected: 400 Bad Request
        /// API message: "No valid orders to pay for."
        /// </summary>
        [Fact]
        public async Task UTCID03_CreatePaymentUrl_InvalidOrders_ShouldReturn400BadRequest()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var orderId1 = Guid.NewGuid();
            var orderId2 = Guid.NewGuid();
            var orderId3 = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid> { orderId1, orderId2, orderId3 },
                Note = "Payment note"
            };

            // Order 1: Not found
            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId1))
                .ReturnsAsync((Order?)null);

            // Order 2: Belongs to different user
            var order2 = new Order
            {
                Id = orderId2,
                CustomerId = Guid.NewGuid(), // Different customer
                Status = OrderStatus.pending,
                TotalAmount = 100000
            };
            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId2))
                .ReturnsAsync(order2);

            // Order 3: Not pending (already approved)
            var order3 = new Order
            {
                Id = orderId3,
                CustomerId = customerId,
                Status = OrderStatus.approved, // Not pending
                TotalAmount = 100000
            };
            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId3))
                .ReturnsAsync(order3);

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var badRequestResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
            Assert.Equal(400, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(objectResult.Value);
            Assert.Equal("No valid orders to pay for.", apiResponse.Message);
            Assert.Null(apiResponse.Data);

            _mockTransactionService.Verify(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()), Times.Never);
            _mockVnpay.Verify(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()), Times.Never);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Null order IDs should return 400 Bad Request
        /// </summary>
        [Fact]
        public async Task Additional_CreatePaymentUrl_NullOrderIds_ShouldReturn400BadRequest()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = null!, // Null
                Note = "Payment note"
            };

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var badRequestResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
            Assert.Equal(400, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(objectResult.Value);
            Assert.Equal("Order IDs are required.", apiResponse.Message);
        }

        /// <summary>
        /// Additional Test: Multiple valid orders should calculate total correctly
        /// </summary>
        [Fact]
        public async Task Additional_CreatePaymentUrl_MultipleValidOrders_ShouldCalculateTotalCorrectly()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var orderId1 = Guid.NewGuid();
            var orderId2 = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid> { orderId1, orderId2 }
            };

            var order1 = new Order
            {
                Id = orderId1,
                CustomerId = customerId,
                Status = OrderStatus.pending,
                TotalAmount = 100000
            };

            var order2 = new Order
            {
                Id = orderId2,
                CustomerId = customerId,
                Status = OrderStatus.pending,
                TotalAmount = 50000
            };

            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId1))
                .ReturnsAsync(order1);
            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId2))
                .ReturnsAsync(order2);

            _mockTransactionService.Setup(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()))
                .ReturnsAsync(It.IsAny<BusinessObject.Models.Transaction>());

            _mockVnpay.Setup(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()))
                .Returns("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html");

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var okResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
            
            _mockTransactionService.Verify(x => x.SaveTransactionAsync(It.Is<BusinessObject.Models.Transaction>(
                t => t.Amount == 150000 // Total of both orders
            )), Times.Once);
        }

        /// <summary>
        /// Additional Test: Mixed valid and invalid orders should process only valid ones
        /// </summary>
        [Fact]
        public async Task Additional_CreatePaymentUrl_MixedOrders_ShouldProcessOnlyValidOnes()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var validOrderId = Guid.NewGuid();
            var invalidOrderId = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid> { validOrderId, invalidOrderId }
            };

            var validOrder = new Order
            {
                Id = validOrderId,
                CustomerId = customerId,
                Status = OrderStatus.pending,
                TotalAmount = 100000
            };

            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(validOrderId))
                .ReturnsAsync(validOrder);

            // Invalid order (not found)
            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(invalidOrderId))
                .ReturnsAsync((Order?)null);

            _mockTransactionService.Setup(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()))
                .ReturnsAsync(It.IsAny<BusinessObject.Models.Transaction>());

            _mockVnpay.Setup(x => x.GetPaymentUrl(It.IsAny<PaymentRequest>()))
                .Returns("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html");

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var okResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
            
            // Should create transaction with only the valid order
            _mockTransactionService.Verify(x => x.SaveTransactionAsync(It.Is<BusinessObject.Models.Transaction>(
                t => t.Amount == 100000 && t.Orders.Count == 1
            )), Times.Once);
        }

        /// <summary>
        /// Additional Test: Exception during transaction save should return 400 Bad Request
        /// </summary>
        [Fact]
        public async Task Additional_CreatePaymentUrl_TransactionSaveException_ShouldReturn400BadRequest()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            SetupUserContext(customerId);

            var requestDto = new CreatePaymentRequestDto
            {
                OrderIds = new List<Guid> { orderId }
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.pending,
                TotalAmount = 100000
            };

            _mockOrderService.Setup(x => x.GetOrderEntityByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockTransactionService.Setup(x => x.SaveTransactionAsync(It.IsAny<BusinessObject.Models.Transaction>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreatePaymentUrl(requestDto);

            // Assert
            var badRequestResult = Assert.IsType<ActionResult<ApiResponse<string>>>(result);
            var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
            Assert.Equal(400, objectResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(objectResult.Value);
            Assert.Equal("Database error", apiResponse.Message);
        }

        #endregion
    }
}

