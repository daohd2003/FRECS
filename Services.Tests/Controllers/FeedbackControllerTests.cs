using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.FeedbackServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for Feedback Controller (API Layer)
    /// Verifies API messages and HTTP status codes
    /// 
    /// Test Coverage:
    /// - Get Feedbacks By Product (GET /api/feedbacks/product/{productId})
    /// 
    /// Note: Controller returns ApiResponse from service
    ///       Service returns ApiResponse<PaginatedResponse<FeedbackResponseDto>>
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~FeedbackControllerTests"
    /// </summary>
    public class FeedbackControllerTests
    {
        private readonly Mock<IFeedbackService> _mockFeedbackService;
        private readonly FeedbackController _controller;

        public FeedbackControllerTests()
        {
            _mockFeedbackService = new Mock<IFeedbackService>();
            _controller = new FeedbackController(_mockFeedbackService.Object);
            
            // Setup mock User context for controller (anonymous user)
            var claims = new List<System.Security.Claims.Claim>();
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region GetFeedbacksByProduct Tests

        /// <summary>
        /// Get Feedbacks By Product - Valid product with feedback
        /// Expected: 200 OK with ApiResponse<PaginatedResponse<FeedbackResponseDto>>
        /// API Message: "Success" (from service)
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_ValidProductIdWithFeedback_ShouldReturn200WithFeedbackList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var page = 1;
            var pageSize = 5;

            var feedbacks = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Test Customer",
                    Rating = 5,
                    Comment = "Great product!",
                    SubmittedAt = DateTime.UtcNow
                }
            };

            var paginatedResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = feedbacks,
                Page = page,
                PageSize = pageSize,
                TotalItems = feedbacks.Count
            };

            var serviceResponse = new ApiResponse<PaginatedResponse<FeedbackResponseDto>>(
                "Success",
                paginatedResponse
            );

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(okResult.Value);
            Assert.Equal("Success", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(1, apiResponse.Data.Items.Count);
            Assert.Equal("Great product!", apiResponse.Data.Items.First().Comment);

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()), Times.Once);
        }

        /// <summary>
        /// Get Feedbacks By Product - Valid product without feedback
        /// Expected: 200 OK with empty list
        /// API Message: "Success" (from service)
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_ValidProductIdWithoutFeedback_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var page = 1;
            var pageSize = 5;

            var paginatedResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = new List<FeedbackResponseDto>(), // Empty list
                Page = page,
                PageSize = pageSize,
                TotalItems = 0
            };

            var serviceResponse = new ApiResponse<PaginatedResponse<FeedbackResponseDto>>(
                "Success",
                paginatedResponse
            );

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(okResult.Value);
            Assert.Equal("Success", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
            Assert.Empty(apiResponse.Data.Items);
            Assert.Equal(0, apiResponse.Data.TotalItems);
        }

        /// <summary>
        /// Get Feedbacks By Product - Invalid product ID or pagination
        /// Expected: 400 BadRequest
        /// API Message: From service (when Data is null)
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_InvalidRequest_ShouldReturn400()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var page = 0; // Invalid page number
            var pageSize = 5;

            var serviceResponse = new ApiResponse<PaginatedResponse<FeedbackResponseDto>>(
                "Invalid page number",
                null // Data is null
            );

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(badRequestResult.Value);
            Assert.Equal("Invalid page number", apiResponse.Message); // Verify API message
            Assert.Null(apiResponse.Data);

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify pagination parameters are passed correctly
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_VerifyPaginationParameters()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var page = 2;
            var pageSize = 10;

            var paginatedResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = new List<FeedbackResponseDto>(),
                Page = page,
                PageSize = pageSize,
                TotalItems = 0
            };

            var serviceResponse = new ApiResponse<PaginatedResponse<FeedbackResponseDto>>(
                "Success",
                paginatedResponse
            );

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize, It.IsAny<Guid?>()))
                .ReturnsAsync(serviceResponse);

            // Act
            await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(
                It.Is<Guid>(id => id == productId),
                It.Is<int>(p => p == page),
                It.Is<int>(ps => ps == pageSize),
                It.IsAny<Guid?>()
            ), Times.Once);
        }

        #endregion

        #region GetFeedbacksByProductAndCustomer Tests (Provider Role - View Customer Comments in Order Detail)

        /// <summary>
        /// UTCID01: Provider views customer feedback for a specific product and customer
        /// Expected: 200 OK with feedback list
        /// API Message: "Feedbacks retrieved successfully."
        /// Use case: Provider viewing customer comments in Order Detail page
        /// </summary>
        [Fact]
        public async Task UTCID01_GetFeedbacksByProductAndCustomer_ProviderView_ShouldReturn200WithFeedbackList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            var feedbacks = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    CustomerName = "Test Customer",
                    Rating = 5,
                    Comment = "Great product, very satisfied!",
                    SubmittedAt = DateTime.UtcNow
                }
            };

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(feedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProductAndCustomer(productId, customerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Feedbacks retrieved successfully.", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Single(returnedFeedbacks);
            Assert.Equal("Great product, very satisfied!", returnedFeedbacks.First().Comment);
            Assert.Equal(customerId, returnedFeedbacks.First().CustomerId);

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId), Times.Once);
        }

        /// <summary>
        /// UTCID02: Provider views customer feedback - no feedback exists
        /// Expected: 200 OK with empty list
        /// API Message: "Feedbacks retrieved successfully."
        /// </summary>
        [Fact]
        public async Task UTCID02_GetFeedbacksByProductAndCustomer_NoFeedback_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var emptyFeedbacks = new List<FeedbackResponseDto>();

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(emptyFeedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProductAndCustomer(productId, customerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Feedbacks retrieved successfully.", apiResponse.Message);
            
            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Empty(returnedFeedbacks);
        }

        /// <summary>
        /// UTCID03: Provider views customer feedback - multiple feedbacks
        /// Expected: 200 OK with multiple feedbacks
        /// </summary>
        [Fact]
        public async Task UTCID03_GetFeedbacksByProductAndCustomer_MultipleFeedbacks_ShouldReturn200WithMultipleFeedbacks()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            var feedbacks = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    Rating = 5,
                    Comment = "First feedback",
                    SubmittedAt = DateTime.UtcNow.AddDays(-5)
                },
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    Rating = 4,
                    Comment = "Second feedback",
                    SubmittedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(feedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProductAndCustomer(productId, customerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Equal(2, returnedFeedbacks.Count());
        }

        #endregion

        #region GetFeedbacksByProviderIdAsync Tests (Provider Role - View All Customer Comments)

        /// <summary>
        /// UTCID04: Provider views all feedbacks for their products/orders
        /// Expected: 200 OK with feedback list
        /// API Message: "Owned feedbacks retrieved successfully."
        /// </summary>
        [Fact]
        public async Task UTCID04_GetFeedbacksByProviderId_ProviderOwnFeedbacks_ShouldReturn200WithFeedbackList()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var feedbacks = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer A",
                    Rating = 5,
                    Comment = "Excellent product!",
                    SubmittedAt = DateTime.UtcNow
                },
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Order,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer B",
                    Rating = 4,
                    Comment = "Good service",
                    SubmittedAt = DateTime.UtcNow
                }
            };

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId, providerId, false))
                .ReturnsAsync(feedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProviderIdAsync(providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Owned feedbacks retrieved successfully.", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Equal(2, returnedFeedbacks.Count());

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProviderIdAsync(providerId, providerId, false), Times.Once);
        }

        /// <summary>
        /// UTCID05: Provider views feedbacks - no feedbacks exist
        /// Expected: 200 OK with empty list
        /// </summary>
        [Fact]
        public async Task UTCID05_GetFeedbacksByProviderId_NoFeedbacks_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var emptyFeedbacks = new List<FeedbackResponseDto>();

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId, providerId, false))
                .ReturnsAsync(emptyFeedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProviderIdAsync(providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Empty(returnedFeedbacks);
        }

        /// <summary>
        /// UTCID06: Provider tries to view another provider's feedbacks - should throw UnauthorizedAccessException
        /// Expected: Service throws UnauthorizedAccessException
        /// </summary>
        [Fact]
        public async Task UTCID06_GetFeedbacksByProviderId_DifferentProvider_ShouldThrowUnauthorized()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var differentProviderId = Guid.NewGuid();
            SetupProvider(providerId);

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProviderIdAsync(differentProviderId, providerId, false))
                .ThrowsAsync(new UnauthorizedAccessException("You are not authorized to view feedbacks of other providers."));

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _controller.GetFeedbacksByProviderIdAsync(differentProviderId));
        }

        /// <summary>
        /// UTCID07: Provider views feedbacks with provider response
        /// Expected: 200 OK with feedbacks including provider responses
        /// </summary>
        [Fact]
        public async Task UTCID07_GetFeedbacksByProviderId_WithProviderResponse_ShouldReturn200WithResponses()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);

            var feedbacks = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer A",
                    Rating = 5,
                    Comment = "Great product!",
                    ProviderResponse = "Thank you for your feedback!",
                    ProviderResponseAt = DateTime.UtcNow,
                    SubmittedAt = DateTime.UtcNow
                }
            };

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId, providerId, false))
                .ReturnsAsync(feedbacks);

            // Act
            var result = await _controller.GetFeedbacksByProviderIdAsync(providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            var returnedFeedbacks = Assert.IsAssignableFrom<IEnumerable<FeedbackResponseDto>>(apiResponse.Data);
            Assert.Single(returnedFeedbacks);
            Assert.NotNull(returnedFeedbacks.First().ProviderResponse);
            Assert.Equal("Thank you for your feedback!", returnedFeedbacks.First().ProviderResponse);
        }

        #endregion

        private void SetupProvider(Guid providerId)
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, providerId.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "provider")
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }
    }
}

