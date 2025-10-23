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
    /// Unit tests for DiscountCodeController - Create Discount Code (API Layer)
    /// Verifies API messages, HTTP status codes, and validation
    /// 
    /// Test Coverage:
    /// - Create Discount Code (POST /api/DiscountCode)
    /// - 8 test cases total (7 core + 1 additional)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~CreateDiscountCodeControllerTests"
    /// </summary>
    public class CreateDiscountCodeControllerTests
    {
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly DiscountCodeController _controller;

        public CreateDiscountCodeControllerTests()
        {
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _controller = new DiscountCodeController(_mockDiscountCodeService.Object);
        }

        /// <summary>
        /// UTCID-01: Create discount code - Success case
        /// Valid, unique code with all valid inputs
        /// Expected: HTTP 201 Created with success message "Discount code created successfully"
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_ValidUniqueCode_ShouldReturn201WithSuccessMessage()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
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
                Id = Guid.NewGuid(),
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            _mockDiscountCodeService.Setup(x => x.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<DiscountCodeDto>>(createdResult.Value);
            Assert.Equal("Discount code created successfully", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("SUMMER25", apiResponse.Data.Code);
            Assert.Equal(25, apiResponse.Data.Value);
        }

        /// <summary>
        /// UTCID-02: Create discount code - Duplicate code
        /// Existing code that already exists in the system
        /// Expected: HTTP 400 BadRequest with error message from service
        /// Exception: InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_DuplicateCode_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "WINTER10",
                DiscountType = DiscountType.Percentage,
                Value = 10,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 50,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            _mockDiscountCodeService.Setup(x => x.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>()))
                .ThrowsAsync(new InvalidOperationException("Discount code 'WINTER10' already exists."));

            // Act
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Discount code 'WINTER10' already exists.", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-03: Create discount code - Blank/Empty code
        /// Code field is empty or null
        /// Expected: HTTP 400 BadRequest with validation error "Code is required"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_BlankCode_ShouldReturn400WithValidationError()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
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
            var result = await _controller.CreateDiscountCode(createDto);

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
        /// UTCID-04: Create discount code - Value equals 0
        /// Value is set to 0 which violates the minimum value constraint
        /// Expected: HTTP 400 BadRequest with validation error "Value must be greater than 0"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_ValueEqualsZero_ShouldReturn400WithValidationError()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
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
            var result = await _controller.CreateDiscountCode(createDto);

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
        /// UTCID-05: Create discount code - Past expiration date
        /// ExpirationDate is set to a date in the past
        /// Expected: HTTP 400 BadRequest with error message "Expiration date must be in the future."
        /// Exception: InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_PastExpirationDate_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "PASTDATE",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(-1), // Past date
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            _mockDiscountCodeService.Setup(x => x.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>()))
                .ThrowsAsync(new InvalidOperationException("Expiration date must be in the future."));

            // Act
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Expiration date must be in the future.", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// UTCID-06: Create discount code - Quantity equals 0
        /// Quantity is set to 0 which violates the minimum quantity constraint
        /// Expected: HTTP 400 BadRequest with validation error "Quantity must be at least 1"
        /// Exception: ArgumentException (model validation)
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_QuantityEqualsZero_ShouldReturn400WithValidationError()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
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
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.True(modelState.ContainsKey("Quantity"));
            var errors = modelState["Quantity"] as string[];
            Assert.NotNull(errors);
            Assert.Contains("Quantity must be at least 1", errors);
        }

        /// <summary>
        /// UTCID-07: Create discount code - Blank UsageType
        /// UsageType field is not provided (validation should catch this)
        /// Expected: HTTP 400 BadRequest with validation error "Usage type is required"
        /// Exception: ArgumentException (model validation)
        /// Note: Since UsageType is an enum with a default value, this test simulates the scenario
        /// where validation would fail if the UsageType were somehow invalid or missing in a different context
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_BlankUsageType_ShouldReturn400WithValidationError()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "NOUSAGE",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active
                // UsageType not set (though it has a default, we simulate validation failure)
            };

            // Simulate model validation error
            _controller.ModelState.AddModelError("UsageType", "Usage type is required");

            // Act
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.True(modelState.ContainsKey("UsageType"));
            var errors = modelState["UsageType"] as string[];
            Assert.NotNull(errors);
            Assert.Contains("Usage type is required", errors);
        }

        /// <summary>
        /// UTCID-08: Create discount code - All fields valid with Fixed discount type
        /// Valid creation with Fixed discount type instead of Percentage
        /// Expected: HTTP 201 Created with success message
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_ValidFixedDiscountType_ShouldReturn201WithSuccessMessage()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "FIXED50",
                DiscountType = DiscountType.Fixed,
                Value = 50,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var expectedResult = new DiscountCodeDto
            {
                Id = Guid.NewGuid(),
                Code = "FIXED50",
                DiscountType = DiscountType.Fixed,
                Value = 50,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            _mockDiscountCodeService.Setup(x => x.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateDiscountCode(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<DiscountCodeDto>>(createdResult.Value);
            Assert.Equal("Discount code created successfully", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(DiscountType.Fixed, apiResponse.Data.DiscountType);
        }
    }
}

