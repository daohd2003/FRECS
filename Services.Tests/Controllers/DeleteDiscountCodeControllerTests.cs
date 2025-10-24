using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DiscountCodeServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for DiscountCodeController - Delete Discount Code (API Layer)
    /// Verifies API messages, HTTP status codes, and validation
    /// 
    /// Test Coverage:
    /// - Delete Discount Code (DELETE /api/DiscountCode/{id})
    /// - 3 test cases total (all core test cases)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~DeleteDiscountCodeControllerTests"
    /// </summary>
    public class DeleteDiscountCodeControllerTests
    {
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly DiscountCodeController _controller;

        public DeleteDiscountCodeControllerTests()
        {
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _controller = new DiscountCodeController(_mockDiscountCodeService.Object);
        }

        /// <summary>
        /// UTCID-01: Delete discount code - Success case
        /// Valid, existing GUID
        /// Expected: HTTP 200 OK with success message "Discount code deleted successfully"
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_ValidExistingGuid_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var discountId = Guid.NewGuid();

            _mockDiscountCodeService.Setup(x => x.DeleteDiscountCodeAsync(discountId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteDiscountCode(discountId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Discount code deleted successfully", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-02: Delete discount code - Non-existent GUID
        /// GUID does not exist in database
        /// Expected: HTTP 404 NotFound with error message "Discount code not found"
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_NonExistentGuid_ShouldReturn404WithErrorMessage()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _mockDiscountCodeService.Setup(x => x.DeleteDiscountCodeAsync(nonExistentId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteDiscountCode(nonExistentId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(notFoundResult.Value);
            Assert.Equal("Discount code not found", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-03: Delete discount code - Code that is in use
        /// Existing GUID of a code that has been used (UsedCount > 0)
        /// Expected: HTTP 400 BadRequest with error message "Cannot delete a discount code that has been used."
        /// Exception: InvalidOperationException
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_CodeInUse_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var discountId = Guid.NewGuid();

            _mockDiscountCodeService.Setup(x => x.DeleteDiscountCodeAsync(discountId))
                .ThrowsAsync(new InvalidOperationException("Cannot delete a discount code that has been used."));

            // Act
            var result = await _controller.DeleteDiscountCode(discountId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Cannot delete a discount code that has been used.", apiResponse.Message); // Verify API message
        }
    }
}

