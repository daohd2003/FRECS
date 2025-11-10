using BusinessObject.Models;
using Moq;
using Repositories.BankAccountRepositories;
using Services.CustomerBankServices;
using AutoMapper;

namespace Services.Tests.BankAccountTests
{
    /// <summary>
    /// Unit tests for CustomerBankService.DeleteBankAccountAsync method
    /// Tests the Remove/Delete Bank Account functionality
    /// </summary>
    public class CustomerBankServiceTests_DeleteBankAccount
    {
        private readonly Mock<IBankAccountRepository> _mockBankAccountRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CustomerBankService _customerBankService;

        public CustomerBankServiceTests_DeleteBankAccount()
        {
            _mockBankAccountRepository = new Mock<IBankAccountRepository>();
            _mockMapper = new Mock<IMapper>();
            _customerBankService = new CustomerBankService(_mockBankAccountRepository.Object, _mockMapper.Object);
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Delete existing bank account
        /// Expected: Bank account deleted successfully, returns true
        /// Backend behavior:
        ///   - Service returns true
        ///   - Controller returns 200 OK
        /// Note: "Bank account deleted successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID01_DeleteBankAccount_ExistingAccount_ShouldReturnTrue()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Vietcombank",
                AccountNumber = "1234567890",
                AccountHolderName = "John Doe",
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            _mockBankAccountRepository.Setup(x => x.DeleteAsync(accountId))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);

            // Assert
            Assert.True(result);

            // Verify GetByIdAndUserAsync was called to check ownership
            _mockBankAccountRepository.Verify(x => x.GetByIdAndUserAsync(accountId, customerId), Times.Once);
            
            // Verify DeleteAsync was called
            _mockBankAccountRepository.Verify(x => x.DeleteAsync(accountId), Times.Once);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Delete non-existent bank account should return false
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_NonExistentAccount_ShouldReturnFalse()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync((BusinessObject.Models.BankAccount?)null);

            // Act
            var result = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);

            // Assert
            Assert.False(result);

            // Verify DeleteAsync was never called
            _mockBankAccountRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Delete bank account belonging to different user should return false
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_DifferentUser_ShouldReturnFalse()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var differentUserId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            // Repository returns null for this customer (account belongs to different user)
            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync((BusinessObject.Models.BankAccount?)null);

            // Act
            var result = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);

            // Assert
            Assert.False(result);

            // Verify DeleteAsync was never called
            _mockBankAccountRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Delete primary bank account should work
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_PrimaryAccount_ShouldReturnTrue()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            var primaryBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "ACB Bank",
                AccountNumber = "9876543210",
                AccountHolderName = "Jane Smith",
                IsPrimary = true // This is the primary account
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(primaryBankAccount);

            _mockBankAccountRepository.Setup(x => x.DeleteAsync(accountId))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);

            // Assert
            Assert.True(result);

            _mockBankAccountRepository.Verify(x => x.DeleteAsync(accountId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify DeleteAsync is called with correct accountId
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_ShouldCallDeleteWithCorrectId()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Test Bank",
                AccountNumber = "1111222233",
                AccountHolderName = "Test User",
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            Guid? capturedAccountId = null;
            _mockBankAccountRepository.Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .Callback<Guid>(id => capturedAccountId = id)
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);

            // Assert
            Assert.True(result);
            Assert.Equal(accountId, capturedAccountId);
        }

        #endregion
    }
}

