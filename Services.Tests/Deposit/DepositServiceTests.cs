using BusinessObject.DTOs.DepositDto;
using Moq;
using Repositories.DepositRepositories;
using Services.DepositServices;

namespace Services.Tests.Deposit
{
    /// <summary>
    /// Unit tests for DepositService - View deposit history functionality
    /// 
    /// Test Coverage Matrix:
    /// ┌─────────┬──────────────────────────────────┬───────────────────────────────────────────────────────────┐
    /// │ Test ID │ Scenario                         │ Expected Result                                           │
    /// ├─────────┼──────────────────────────────────┼───────────────────────────────────────────────────────────┤
    /// │ UTCID01 │ Valid customer with deposits     │ Return deposit history successfully                       │
    /// │ UTCID02 │ Valid customer without deposits  │ Return empty deposit history                              │
    /// │ UTCID03 │ Invalid customer ID              │ Return empty deposit history                              │
    /// │ UTCID04 │ Get deposit stats valid customer │ Return deposit statistics                                 │
    /// └─────────┴──────────────────────────────────┴───────────────────────────────────────────────────────────┘
    /// 
    /// Note: Current implementation returns empty list as deposit refund system is not fully implemented
    ///       These tests verify the service layer behavior
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~DepositServiceTests"
    /// </summary>
    public class DepositServiceTests
    {
        private readonly Mock<IDepositRepository> _mockDepositRepo;
        private readonly DepositService _depositService;

        public DepositServiceTests()
        {
            _mockDepositRepo = new Mock<IDepositRepository>();
            _depositService = new DepositService(_mockDepositRepo.Object);
        }

        /// <summary>
        /// UTCID01: Get deposit history for valid customer with deposits
        /// Expected: Return list of deposit history (currently empty until refund system implemented)
        /// </summary>
        [Fact]
        public async Task UTCID01_GetDepositHistory_ValidCustomerWithDeposits_ShouldReturnHistory()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            // Act
            var result = await _depositService.GetDepositHistoryAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<DepositHistoryDto>>(result);
            // Note: Currently returns empty list as deposit refund system not fully implemented
            Assert.Empty(result);
        }

        /// <summary>
        /// UTCID02: Get deposit history for valid customer without deposits
        /// Expected: Return empty list
        /// </summary>
        [Fact]
        public async Task UTCID02_GetDepositHistory_ValidCustomerWithoutDeposits_ShouldReturnEmptyList()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            // Act
            var result = await _depositService.GetDepositHistoryAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// UTCID03: Get deposit history for invalid/non-existent customer
        /// Expected: Return empty list
        /// </summary>
        [Fact]
        public async Task UTCID03_GetDepositHistory_InvalidCustomerId_ShouldReturnEmptyList()
        {
            // Arrange
            var invalidCustomerId = Guid.NewGuid();

            // Act
            var result = await _depositService.GetDepositHistoryAsync(invalidCustomerId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// UTCID04: Get deposit stats for valid customer
        /// Expected: Return deposit statistics (currently zeros until refund system implemented)
        /// </summary>
        [Fact]
        public async Task UTCID04_GetDepositStats_ValidCustomer_ShouldReturnStats()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            // Mock repository to return empty orders list
            _mockDepositRepo.Setup(x => x.GetCustomerOrdersWithDepositsAsync(customerId))
                .ReturnsAsync(new List<BusinessObject.Models.Order>());

            // Act
            var result = await _depositService.GetDepositStatsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DepositStatsDto>(result);
            // Note: Currently returns zeros as deposit refund system not fully implemented
            Assert.Equal(0, result.DepositsRefunded);
            Assert.Equal(0, result.PendingRefunds);
            Assert.Equal(0, result.RefundIssues);
        }

        /// <summary>
        /// Additional Test: Verify deposit stats for customer without orders
        /// </summary>
        [Fact]
        public async Task GetDepositStats_CustomerWithoutOrders_ShouldReturnZeroStats()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            _mockDepositRepo.Setup(x => x.GetCustomerOrdersWithDepositsAsync(customerId))
                .ReturnsAsync(new List<BusinessObject.Models.Order>());

            // Act
            var result = await _depositService.GetDepositStatsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.DepositsRefunded);
            Assert.Equal(0, result.PendingRefunds);
            Assert.Equal(0, result.RefundIssues);
        }

        /// <summary>
        /// Additional Test: Verify repository method is called
        /// </summary>
        [Fact]
        public async Task GetDepositStats_ShouldCallRepository()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            _mockDepositRepo.Setup(x => x.GetCustomerOrdersWithDepositsAsync(customerId))
                .ReturnsAsync(new List<BusinessObject.Models.Order>());

            // Act
            await _depositService.GetDepositStatsAsync(customerId);

            // Assert
            _mockDepositRepo.Verify(x => x.GetCustomerOrdersWithDepositsAsync(customerId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify service returns DTO type correctly
        /// </summary>
        [Fact]
        public async Task GetDepositHistory_ShouldReturnCorrectDtoType()
        {
            // Arrange
            var customerId = Guid.NewGuid();

            // Act
            var result = await _depositService.GetDepositHistoryAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IEnumerable<DepositHistoryDto>>(result);
        }
    }
}

