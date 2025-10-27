using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Models;
using Moq;
using Repositories.BankAccountRepositories;
using Services.CustomerBankServices;

namespace Services.Tests.BankAccountTests
{
    /// <summary>
    /// Unit tests for View Bank Account Information functionality (Service Layer)
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬─────────────────────────────────────────────────┬────────────────────────────────┐
    /// │ Test ID │ Input                                           │ Expected Result                │
    /// ├─────────┼─────────────────────────────────────────────────┼────────────────────────────────┤
    /// │ UTCID01 │ Has multiple accounts, one of which is primary │ Return list with primary flag  │
    /// │ UTCID02 │ Has multiple accounts, none are primary        │ Return list without primary    │
    /// │ UTCID03 │ Has no bank accounts                           │ Return empty list              │
    /// └─────────┴─────────────────────────────────────────────────┴────────────────────────────────┘
    /// 
    /// Note: Service layer returns List<BankAccountDto>
    ///       Controller returns Ok(accounts) directly (no ApiResponse wrapper)
    ///       Message "No Refund Accounts" is a FRONTEND message (not from API)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~CustomerBankServiceTests"
    /// </summary>
    public class CustomerBankServiceTests
    {
        private readonly Mock<IBankAccountRepository> _mockBankAccountRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CustomerBankService _customerBankService;

        public CustomerBankServiceTests()
        {
            _mockBankAccountRepository = new Mock<IBankAccountRepository>();
            _mockMapper = new Mock<IMapper>();
            _customerBankService = new CustomerBankService(_mockBankAccountRepository.Object, _mockMapper.Object);
        }

        /// <summary>
        /// UTCID01: Has multiple accounts, one of which is primary
        /// Expected: Return list with primary flag
        /// Backend Service: Returns List<BankAccountDto> with IsPrimary = true for one account
        /// Controller: Returns 200 OK with list
        /// </summary>
        [Fact]
        public async Task UTCID01_GetBankAccounts_MultipleAccountsWithPrimary_ShouldReturnListWithPrimaryFlag()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var bankAccounts = new List<BankAccount>
            {
                new BankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    RoutingNumber = "VCB123",
                    IsPrimary = true // Primary account
                },
                new BankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    RoutingNumber = "TCB456",
                    IsPrimary = false // Not primary
                }
            };

            var bankAccountDtos = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = bankAccounts[0].Id,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    RoutingNumber = "VCB123",
                    IsPrimary = true
                },
                new BankAccountDto
                {
                    Id = bankAccounts[1].Id,
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    RoutingNumber = "TCB456",
                    IsPrimary = false
                }
            };

            _mockBankAccountRepository.Setup(x => x.GetBankAccountsByUserAsync(customerId))
                .ReturnsAsync(bankAccounts);
            _mockMapper.Setup(m => m.Map<List<BankAccountDto>>(bankAccounts))
                .Returns(bankAccountDtos);

            // Act
            var result = await _customerBankService.GetBankAccountsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Single(result.Where(a => a.IsPrimary)); // Only one primary
            Assert.Equal("Vietcombank", result.First(a => a.IsPrimary).BankName);

            _mockBankAccountRepository.Verify(x => x.GetBankAccountsByUserAsync(customerId), Times.Once);
        }

        /// <summary>
        /// UTCID02: Has multiple accounts, none are primary
        /// Expected: Return list without primary flag
        /// Backend Service: Returns List<BankAccountDto> with IsPrimary = false for all
        /// Controller: Returns 200 OK with list
        /// </summary>
        [Fact]
        public async Task UTCID02_GetBankAccounts_MultipleAccountsNoPrimary_ShouldReturnListWithoutPrimary()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var bankAccounts = new List<BankAccount>
            {
                new BankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    RoutingNumber = "VCB123",
                    IsPrimary = false // Not primary
                },
                new BankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    RoutingNumber = "TCB456",
                    IsPrimary = false // Not primary
                }
            };

            var bankAccountDtos = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = bankAccounts[0].Id,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    RoutingNumber = "VCB123",
                    IsPrimary = false
                },
                new BankAccountDto
                {
                    Id = bankAccounts[1].Id,
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    RoutingNumber = "TCB456",
                    IsPrimary = false
                }
            };

            _mockBankAccountRepository.Setup(x => x.GetBankAccountsByUserAsync(customerId))
                .ReturnsAsync(bankAccounts);
            _mockMapper.Setup(m => m.Map<List<BankAccountDto>>(bankAccounts))
                .Returns(bankAccountDtos);

            // Act
            var result = await _customerBankService.GetBankAccountsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Empty(result.Where(a => a.IsPrimary)); // No primary accounts
            Assert.All(result, a => Assert.False(a.IsPrimary));

            _mockBankAccountRepository.Verify(x => x.GetBankAccountsByUserAsync(customerId), Times.Once);
        }

        /// <summary>
        /// UTCID03: Has no bank accounts
        /// Expected: Return empty list
        /// Backend Service: Returns empty List<BankAccountDto>
        /// Controller: Returns 200 OK with empty list
        /// Frontend Message: "No Refund Accounts" (FE only - NOT verified here)
        /// </summary>
        [Fact]
        public async Task UTCID03_GetBankAccounts_NoAccounts_ShouldReturnEmptyList()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var emptyBankAccounts = new List<BankAccount>(); // No accounts
            var emptyBankAccountDtos = new List<BankAccountDto>();

            _mockBankAccountRepository.Setup(x => x.GetBankAccountsByUserAsync(customerId))
                .ReturnsAsync(emptyBankAccounts);
            _mockMapper.Setup(m => m.Map<List<BankAccountDto>>(emptyBankAccounts))
                .Returns(emptyBankAccountDtos);

            // Act
            var result = await _customerBankService.GetBankAccountsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result); // Should return empty list, not null

            _mockBankAccountRepository.Verify(x => x.GetBankAccountsByUserAsync(customerId), Times.Once);

            // Frontend displays: "No Refund Accounts" (FE message, not from API)
        }

        /// <summary>
        /// Additional Test: Verify Last4Digits computed property
        /// </summary>
        [Fact]
        public async Task Additional_GetBankAccounts_ShouldMapLast4DigitsCorrectly()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var bankAccounts = new List<BankAccount>
            {
                new BankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    IsPrimary = true
                }
            };

            var bankAccountDtos = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = bankAccounts[0].Id,
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    IsPrimary = true
                }
            };

            _mockBankAccountRepository.Setup(x => x.GetBankAccountsByUserAsync(customerId))
                .ReturnsAsync(bankAccounts);
            _mockMapper.Setup(m => m.Map<List<BankAccountDto>>(bankAccounts))
                .Returns(bankAccountDtos);

            // Act
            var result = await _customerBankService.GetBankAccountsAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("7890", result.First().Last4Digits); // Last 4 digits of "1234567890"
        }
    }
}

