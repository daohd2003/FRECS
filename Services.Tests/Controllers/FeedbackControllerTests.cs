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

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
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

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize), Times.Once);
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

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
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

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(badRequestResult.Value);
            Assert.Equal("Invalid page number", apiResponse.Message); // Verify API message
            Assert.Null(apiResponse.Data);

            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize), Times.Once);
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

            _mockFeedbackService.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(serviceResponse);

            // Act
            await _controller.GetFeedbacksByProduct(productId, page, pageSize);

            // Assert
            _mockFeedbackService.Verify(x => x.GetFeedbacksByProductAsync(
                It.Is<Guid>(id => id == productId),
                It.Is<int>(p => p == page),
                It.Is<int>(ps => ps == pageSize)
            ), Times.Once);
        }

        #endregion
    }
}

