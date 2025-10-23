using AutoMapper;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.DiscountCodeRepositories;
using Services.DiscountCodeServices;

namespace Services.Tests.DiscountCode
{
    /// <summary>
    /// Unit tests for DiscountCodeService - Update Discount Code functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬──────────────────────────────────┬───────────────────────────────────────────────────────────┬──────────────────────┐
    /// │ Test ID │ Scenario                         │ Expected Result                                           │ Exception Type       │
    /// ├─────────┼──────────────────────────────────┼───────────────────────────────────────────────────────────┼──────────────────────┤
    /// │ UTCID01 │ Valid update                     │ Return updated DiscountCodeDto successfully               │ No exception         │
    /// │ UTCID02 │ Non-existent GUID                │ Return null                                               │ No exception         │
    /// │ UTCID03 │ Duplicate code                   │ InvalidOperationException: "Discount code ... exists."    │ InvalidOperationEx   │
    /// └─────────┴──────────────────────────────────┴───────────────────────────────────────────────────────────┴──────────────────────┘
    /// 
    /// Note: Service layer tests business logic and exception messages.
    ///       Model validation (blank code, value=0, quantity=0) is tested at Controller layer.
    ///       
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~UpdateDiscountCodeServiceTests"
    /// </summary>
    public class UpdateDiscountCodeServiceTests
    {
        private readonly Mock<IDiscountCodeRepository> _mockDiscountCodeRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly DiscountCodeService _discountCodeService;

        public UpdateDiscountCodeServiceTests()
        {
            _mockDiscountCodeRepository = new Mock<IDiscountCodeRepository>();
            _mockMapper = new Mock<IMapper>();

            _discountCodeService = new DiscountCodeService(
                _mockDiscountCodeRepository.Object,
                _mockMapper.Object
            );
        }

        /// <summary>
        /// UTCID-01: Update discount code - Success case
        /// Valid existing GUID with valid update data
        /// Expected: Successfully update discount code and return DiscountCodeDto
        /// </summary>
        [Fact]
        public async Task UTCID01_UpdateDiscountCode_ValidUpdate_ShouldReturnDiscountCodeDto()
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

            var existingEntity = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "OLDCODE",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = DateTime.UtcNow.AddDays(10),
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var updatedEntity = new BusinessObject.Models.DiscountCode
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

            var expectedDto = new DiscountCodeDto
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
                CreatedAt = updatedEntity.CreatedAt,
                UpdatedAt = updatedEntity.UpdatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(existingEntity);

            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(updateDto.Code, discountId))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map(updateDto, existingEntity))
                .Callback<UpdateDiscountCodeDto, BusinessObject.Models.DiscountCode>((src, dest) =>
                {
                    dest.Code = src.Code;
                    dest.Value = src.Value;
                    dest.Quantity = src.Quantity;
                });

            _mockDiscountCodeRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.UpdateDiscountCodeAsync(discountId, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SUMMER25", result.Code);
            Assert.Equal(25, result.Value);
            Assert.Equal(100, result.Quantity);

            // Verify repository was called
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(discountId), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.IsCodeUniqueAsync(updateDto.Code, discountId), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Once);
        }

        /// <summary>
        /// UTCID-02: Update discount code - Non-existent GUID
        /// Discount code with given ID does not exist
        /// Expected: Return null
        /// </summary>
        [Fact]
        public async Task UTCID02_UpdateDiscountCode_NonExistentGuid_ShouldReturnNull()
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

            // Setup mock - discount code not found
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(nonExistentId))
                .ReturnsAsync((BusinessObject.Models.DiscountCode?)null);

            // Act
            var result = await _discountCodeService.UpdateDiscountCodeAsync(nonExistentId, updateDto);

            // Assert
            Assert.Null(result);

            // Verify repository was called to find the discount code
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(nonExistentId), Times.Once);
            
            // Verify UpdateAsync was NOT called since discount code doesn't exist
            _mockDiscountCodeRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Never);
        }

        /// <summary>
        /// UTCID-03: Update discount code - Duplicate code
        /// Trying to update with a code that already exists for another discount code
        /// Expected: Throw InvalidOperationException with message "Discount code 'WINTER10' already exists."
        /// </summary>
        [Fact]
        public async Task UTCID03_UpdateDiscountCode_DuplicateCode_ShouldThrowInvalidOperationException()
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

            var existingEntity = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "OLDCODE",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = DateTime.UtcNow.AddDays(10),
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(existingEntity);

            // Setup mock - code is NOT unique (already exists for another discount code)
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(updateDto.Code, discountId))
                .ReturnsAsync(false);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _discountCodeService.UpdateDiscountCodeAsync(discountId, updateDto)
            );

            // Verify the exact exception message
            Assert.Equal("Discount code 'WINTER10' already exists.", exception.Message);

            // Verify repository was called
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(discountId), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.IsCodeUniqueAsync(updateDto.Code, discountId), Times.Once);
            
            // Verify UpdateAsync was NOT called since validation failed
            _mockDiscountCodeRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Update discount code - Same code (no conflict)
        /// Updating with the same code as the current code (should be allowed)
        /// Expected: Successfully update
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_SameCode_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "SUMMER25", // Same code
                DiscountType = DiscountType.Percentage,
                Value = 30, // Changed value
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 150, // Changed quantity
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var existingEntity = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "SUMMER25", // Same code
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(20),
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountId,
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 30,
                ExpirationDate = updateDto.ExpirationDate,
                Quantity = 150,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = existingEntity.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(existingEntity);

            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(updateDto.Code, discountId))
                .ReturnsAsync(true); // Unique when excluding current ID

            _mockMapper.Setup(x => x.Map(updateDto, existingEntity))
                .Callback<UpdateDiscountCodeDto, BusinessObject.Models.DiscountCode>((src, dest) =>
                {
                    dest.Value = src.Value;
                    dest.Quantity = src.Quantity;
                });

            _mockDiscountCodeRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.UpdateDiscountCodeAsync(discountId, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SUMMER25", result.Code);
            Assert.Equal(30, result.Value);
            Assert.Equal(150, result.Quantity);
        }

        /// <summary>
        /// Additional Test: Update discount code - Change discount type
        /// Update from Percentage to Fixed
        /// Expected: Successfully update
        /// </summary>
        [Fact]
        public async Task UpdateDiscountCode_ChangeDiscountType_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var updateDto = new UpdateDiscountCodeDto
            {
                Code = "NEWTYPE",
                DiscountType = DiscountType.Fixed, // Changed to Fixed
                Value = 100,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 50,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var existingEntity = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "NEWTYPE",
                DiscountType = DiscountType.Percentage, // Was Percentage
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(20),
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountId,
                Code = "NEWTYPE",
                DiscountType = DiscountType.Fixed,
                Value = 100,
                ExpirationDate = updateDto.ExpirationDate,
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = existingEntity.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(existingEntity);

            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(updateDto.Code, discountId))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map(updateDto, existingEntity))
                .Callback<UpdateDiscountCodeDto, BusinessObject.Models.DiscountCode>((src, dest) =>
                {
                    dest.DiscountType = src.DiscountType;
                    dest.Value = src.Value;
                });

            _mockDiscountCodeRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.UpdateDiscountCodeAsync(discountId, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DiscountType.Fixed, result.DiscountType);
            Assert.Equal(100, result.Value);
        }
    }
}

