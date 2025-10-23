using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DiscountCodeServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for DiscountCodeController - Update Discount Code (API Layer)
    /// Verifies API messages, HTTP status codes, and validation
    /// 
    /// Test Coverage:
    /// - Update Discount Code (PUT /api/DiscountCode/{id})
    /// - 6 test cases total (all core test cases)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~UpdateDiscountCodeControllerTests"
    /// </summary>
    public class UpdateDiscountCodeControllerTests
    {
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly DiscountCodeController _controller;

        public UpdateDiscountCodeControllerTests()
        {
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _controller = new DiscountCodeController(_mockDiscountCodeService.Object);
        }

        /// <summary>
        /// UTCID-01: Update discount code - Success case
        /// Valid existing GUID with valid updates
        /// Expected: HTTP 200 OK with success message "Discount code updated successfully"
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_ValidUpdate_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var expectedResult = new DiscountCodeDto
            {
                Id = discountId,
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = updateDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            };

            _mockDiscountCodeService.Setup(x => x.UpdateDiscountCodeAsync(discountId, It.IsAny<UpdateDiscountCodeDto>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.UpdateDiscountCode(discountId, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<DiscountCodeDto>>(okResult.Value);
            Assert.Equal("Discount code updated successfully", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("SUMMER25", apiResponse.Data.Code);
            Assert.Equal(25, apiResponse.Data.Value);
        }

        /// <summary>
        /// UTCID-02: Update discount code - Non-existent GUID
        /// GUID does not exist in database
        /// Expected: HTTP 404 NotFound with error message "Discount code not found"
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_NonExistentGuid_ShouldReturn404WithErrorMessage()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            _mockDiscountCodeService.Setup(x => x.UpdateDiscountCodeAsync(nonExistentId, It.IsAny<UpdateDiscountCodeDto>()))
                .ReturnsAsync((DiscountCodeDto?)null);

            // Act
            var result = await _controller.UpdateDiscountCode(nonExistentId, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(notFoundResult.Value);
            Assert.Equal("Discount code not found", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-03: Update discount code - Duplicate code
        /// Trying to update with a code that already exists for another discount code
        /// Expected: HTTP 400 BadRequest with error message "Discount code 'WINTER10' already exists."
        /// Exception: InvalidOperationException
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_DuplicateCode_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "WINTER10", // Code already exists for another discount code
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            _mockDiscountCodeService.Setup(x => x.UpdateDiscountCodeAsync(discountId, It.IsAny<UpdateDiscountCodeDto>()))
                .ThrowsAsync(new InvalidOperationException("Discount code 'WINTER10' already exists."));

            // Act
            var result = await _controller.UpdateDiscountCode(discountId, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Discount code 'WINTER10' already exists.", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-04: Update discount code - Blank/Empty code
        /// Code field is empty or null
        /// Expected: HTTP 400 BadRequest with validation error "Discount code is required"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_BlankCode_ShouldReturn400WithValidationError()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "", // Blank code
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            // Simulate model validation error
            _controller.ModelState.AddModelError("Code", "Discount code is required");

            // Act
            var result = await _controller.UpdateDiscountCode(discountId, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Verify that ModelState contains the validation error
            var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.True(modelState.ContainsKey("Code"));
            var errors = modelState["Code"] as string[];
            Assert.NotNull(errors);
            Assert.Contains("Discount code is required", errors);
        }

        /// <summary>
        /// UTCID-05: Update discount code - Value equals 0
        /// Value is set to 0 which violates the minimum value constraint
        /// Expected: HTTP 400 BadRequest with validation error "Value must be greater than 0"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_ValueEqualsZero_ShouldReturn400WithValidationError()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "TESTCODE",
                DiscountType = DiscountType.Percentage,
                Value = 0, // Invalid value
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            // Simulate model validation error
            _controller.ModelState.AddModelError("Value", "Value must be greater than 0");

            // Act
            var result = await _controller.UpdateDiscountCode(discountId, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.True(modelState.ContainsKey("Value"));
            var errors = modelState["Value"] as string[];
            Assert.NotNull(errors);
            Assert.Contains("Value must be greater than 0", errors);
        }

        /// <summary>
        /// UTCID-06: Update discount code - Quantity equals 0
        /// Quantity is set to 0 which violates the minimum quantity constraint
        /// Expected: HTTP 400 BadRequest with validation error "Quantity must be at least 1"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_QuantityEqualsZero_ShouldReturn400WithValidationError()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "ZEROQTY",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 0, // Invalid quantity
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            // Simulate model validation error
            _controller.ModelState.AddModelError("Quantity", "Quantity must be at least 1");

            // Act
            var result = await _controller.UpdateDiscountCode(discountId, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.True(modelState.ContainsKey("Quantity"));
            var errors = modelState["Quantity"] as string[];
            Assert.NotNull(errors);
            Assert.Contains("Quantity must be at least 1", errors);
        }
    }
}

