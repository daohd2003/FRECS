using AutoMapper;
using BusinessObject.Enums;
using Repositories.DiscountCodeRepositories;
using Services.DiscountCodeServices;

namespace Services.Tests.DiscountCode
{
    /// <summary>
    /// Unit tests for DiscountCodeService - Delete Discount Code functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬──────────────────────────────────┬───────────────────────────────────────────────────────────┬──────────────────────┐
    /// │ Test ID │ Scenario                         │ Expected Result                                           │ Exception Type       │
    /// ├─────────┼──────────────────────────────────┼───────────────────────────────────────────────────────────┼──────────────────────┤
    /// │ UTCID01 │ Valid, existing GUID             │ Return true, discount code deleted                        │ No exception         │
    /// │ UTCID02 │ Non-existent GUID                │ Return false                                              │ No exception         │
    /// │ UTCID03 │ Code in use (UsedCount > 0)      │ InvalidOperationException: "Cannot delete ... used."      │ InvalidOperationEx   │
    /// └─────────┴──────────────────────────────────┴───────────────────────────────────────────────────────────┴──────────────────────┘
    /// 
    /// Note: Service layer tests business logic and exception messages.
    ///       
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~DeleteDiscountCodeServiceTests"
    /// </summary>
    public class DeleteDiscountCodeServiceTests
    {
        private readonly Mock<IDiscountCodeRepository> _mockDiscountCodeRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly DiscountCodeService _discountCodeService;

        public DeleteDiscountCodeServiceTests()
        {
            _mockDiscountCodeRepository = new Mock<IDiscountCodeRepository>();
            _mockMapper = new Mock<IMapper>();

            _discountCodeService = new DiscountCodeService(
                _mockDiscountCodeRepository.Object,
                _mockMapper.Object
            );
        }

        /// <summary>
        /// UTCID-01: Delete discount code - Success case
        /// Valid, existing GUID with no usage
        /// Expected: Successfully delete discount code and return true
        /// </summary>
        [Fact]
        public async Task UTCID01_DeleteDiscountCode_ValidExistingGuid_ShouldReturnTrue()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var existingDiscountCode = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                UsedCount = 0, // Not used yet
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(existingDiscountCode);

            _mockDiscountCodeRepository.Setup(x => x.DeleteAsync(discountId))
                .ReturnsAsync(true);

            // Act
            var result = await _discountCodeService.DeleteDiscountCodeAsync(discountId);

            // Assert
            Assert.True(result);

            // Verify repository was called
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(discountId), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(discountId), Times.Once);
        }

        /// <summary>
        /// UTCID-02: Delete discount code - Non-existent GUID
        /// GUID does not exist in database
        /// Expected: Return false
        /// </summary>
        [Fact]
        public async Task UTCID02_DeleteDiscountCode_NonExistentGuid_ShouldReturnFalse()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Setup mock - discount code not found
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(nonExistentId))
                .ReturnsAsync((BusinessObject.Models.DiscountCode?)null);

            // Act
            var result = await _discountCodeService.DeleteDiscountCodeAsync(nonExistentId);

            // Assert
            Assert.False(result);

            // Verify repository was called to find the discount code
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(nonExistentId), Times.Once);
            
            // Verify DeleteAsync was NOT called since discount code doesn't exist
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID-03: Delete discount code - Code that is in use
        /// Discount code has UsedCount > 0 (has been used)
        /// Expected: Throw InvalidOperationException with message "Cannot delete a discount code that has been used."
        /// </summary>
        [Fact]
        public async Task UTCID03_DeleteDiscountCode_CodeInUse_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var usedDiscountCode = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "USED25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                UsedCount = 15, // Has been used 15 times
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Setup mock
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(usedDiscountCode);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _discountCodeService.DeleteDiscountCodeAsync(discountId)
            );

            // Verify the exact exception message
            Assert.Equal("Cannot delete a discount code that has been used.", exception.Message);

            // Verify repository was called to get the discount code
            _mockDiscountCodeRepository.Verify(x => x.GetByIdAsync(discountId), Times.Once);
            
            // Verify DeleteAsync was NOT called since validation failed
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Delete discount code - Code with UsedCount exactly 1
        /// Boundary test: discount code used exactly once
        /// Expected: Throw InvalidOperationException
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_CodeUsedOnce_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var usedDiscountCode = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "ONCE",
                DiscountType = DiscountType.Percentage,
                Value = 10,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                UsedCount = 1, // Used exactly once (boundary case)
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Setup mock
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(usedDiscountCode);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _discountCodeService.DeleteDiscountCodeAsync(discountId)
            );

            // Verify the exception message
            Assert.Equal("Cannot delete a discount code that has been used.", exception.Message);

            // Verify DeleteAsync was NOT called
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Delete discount code - Inactive status but not used
        /// Discount code with Inactive status but UsedCount = 0
        /// Expected: Successfully delete (status doesn't matter, only UsedCount)
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_InactiveButNotUsed_ShouldReturnTrue()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var inactiveDiscountCode = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "INACTIVE",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                UsedCount = 0, // Not used
                Status = DiscountStatus.Inactive, // Inactive status
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(inactiveDiscountCode);

            _mockDiscountCodeRepository.Setup(x => x.DeleteAsync(discountId))
                .ReturnsAsync(true);

            // Act
            var result = await _discountCodeService.DeleteDiscountCodeAsync(discountId);

            // Assert
            Assert.True(result);

            // Verify DeleteAsync was called
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(discountId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Delete discount code - Expired code but not used
        /// Discount code that has expired but was never used
        /// Expected: Successfully delete
        /// </summary>
        [Fact]
        public async Task DeleteDiscountCode_ExpiredButNotUsed_ShouldReturnTrue()
        {
            // Arrange
            var discountId = Guid.NewGuid();
            var expiredDiscountCode = new BusinessObject.Models.DiscountCode
            {
                Id = discountId,
                Code = "EXPIRED",
                DiscountType = DiscountType.Percentage,
                Value = 15,
                ExpirationDate = DateTime.UtcNow.AddDays(-5), // Expired 5 days ago
                Quantity = 100,
                UsedCount = 0, // Not used
                Status = DiscountStatus.Expired,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.GetByIdAsync(discountId))
                .ReturnsAsync(expiredDiscountCode);

            _mockDiscountCodeRepository.Setup(x => x.DeleteAsync(discountId))
                .ReturnsAsync(true);

            // Act
            var result = await _discountCodeService.DeleteDiscountCodeAsync(discountId);

            // Assert
            Assert.True(result);

            // Verify DeleteAsync was called
            _mockDiscountCodeRepository.Verify(x => x.DeleteAsync(discountId), Times.Once);
        }
    }
}

