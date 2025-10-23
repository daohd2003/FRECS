using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Models;
using Moq;
using Repositories.BankAccountRepositories;
using Services.CustomerBankServices;
using AutoMapper;

namespace Services.Tests.BankAccountTests
{
    /// <summary>
    /// Unit tests for CustomerBankService.UpdateBankAccountAsync method
    /// Tests the Edit/Update Bank Account functionality
    /// </summary>
    public class CustomerBankServiceTests_UpdateBankAccount
    {
        private readonly Mock<IBankAccountRepository> _mockBankAccountRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CustomerBankService _customerBankService;

        public CustomerBankServiceTests_UpdateBankAccount()
        {
            _mockBankAccountRepository = new Mock<IBankAccountRepository>();
            _mockMapper = new Mock<IMapper>();
            _customerBankService = new CustomerBankService(_mockBankAccountRepository.Object, _mockMapper.Object);
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Update bank account with SetAsPrimary = TRUE
        /// Expected: Bank account updated successfully with IsPrimary = true, and RemovePrimaryStatusAsync is called
        /// Backend behavior:
        ///   - Service returns true
        ///   - Controller returns 200 OK
        /// Note: "Bank account updated successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID01_UpdateBankAccount_SetAsPrimaryTrue_ShouldUpdateAndRemoveOtherPrimaryStatus()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated Bank",
                AccountNumber = "9999888877",
                AccountHolderName = "Updated Name",
                RoutingNumber = "999888777",
                SetAsPrimary = true
            };

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Old Bank",
                AccountNumber = "1111222233",
                AccountHolderName = "Old Name",
                RoutingNumber = "111222333",
                IsPrimary = false // Currently NOT primary
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            _mockBankAccountRepository.Setup(x => x.RemovePrimaryStatusAsync(customerId))
                .Returns(Task.CompletedTask);

            _mockBankAccountRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.True(result);

            // Verify account properties were updated
            Assert.Equal(updateDto.BankName, existingBankAccount.BankName);
            Assert.Equal(updateDto.AccountNumber, existingBankAccount.AccountNumber);
            Assert.Equal(updateDto.AccountHolderName, existingBankAccount.AccountHolderName);
            Assert.Equal(updateDto.RoutingNumber, existingBankAccount.RoutingNumber);
            Assert.True(existingBankAccount.IsPrimary);

            // Verify RemovePrimaryStatusAsync was called because SetAsPrimary = true and account was not primary before
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(customerId), Times.Once);
            _mockBankAccountRepository.Verify(x => x.UpdateAsync(existingBankAccount), Times.Once);
        }

        /// <summary>
        /// UTCID02: Update bank account with SetAsPrimary = FALSE
        /// Expected: Bank account updated successfully with IsPrimary unchanged, and RemovePrimaryStatusAsync is NOT called
        /// Backend behavior:
        ///   - Service returns true
        ///   - Controller returns 200 OK
        /// Note: "Bank account updated successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID02_UpdateBankAccount_SetAsPrimaryFalse_ShouldUpdateWithoutChangingPrimaryStatus()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated Bank 2",
                AccountNumber = "8888777766",
                AccountHolderName = "Updated Name 2",
                RoutingNumber = "888777666",
                SetAsPrimary = false
            };

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Old Bank 2",
                AccountNumber = "2222333344",
                AccountHolderName = "Old Name 2",
                RoutingNumber = "222333444",
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            _mockBankAccountRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.True(result);

            // Verify account properties were updated
            Assert.Equal(updateDto.BankName, existingBankAccount.BankName);
            Assert.Equal(updateDto.AccountNumber, existingBankAccount.AccountNumber);
            Assert.Equal(updateDto.AccountHolderName, existingBankAccount.AccountHolderName);
            Assert.Equal(updateDto.RoutingNumber, existingBankAccount.RoutingNumber);
            Assert.False(existingBankAccount.IsPrimary); // Remains false

            // Verify RemovePrimaryStatusAsync was NOT called because SetAsPrimary = false
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(It.IsAny<Guid>()), Times.Never);
            _mockBankAccountRepository.Verify(x => x.UpdateAsync(existingBankAccount), Times.Once);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Update bank account that doesn't exist should return false
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_NonExistentAccount_ShouldReturnFalse()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync((BusinessObject.Models.BankAccount?)null);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.False(result);

            // Verify UpdateAsync was never called
            _mockBankAccountRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.BankAccount>()), Times.Never);
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Update bank account that belongs to different user should return false
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_DifferentUser_ShouldReturnFalse()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var differentUserId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            // Repository returns null for this customer (account belongs to different user)
            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync((BusinessObject.Models.BankAccount?)null);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Additional Test: Update account that is already primary with SetAsPrimary = TRUE should NOT call RemovePrimaryStatusAsync
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_AlreadyPrimary_SetAsPrimaryTrue_ShouldNotCallRemovePrimaryStatus()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated Bank",
                AccountNumber = "9999888877",
                AccountHolderName = "Updated Name",
                SetAsPrimary = true
            };

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Old Bank",
                AccountNumber = "1111222233",
                AccountHolderName = "Old Name",
                IsPrimary = true // Already primary
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            _mockBankAccountRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.True(result);
            Assert.True(existingBankAccount.IsPrimary);

            // Verify RemovePrimaryStatusAsync was NOT called because account was already primary
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(It.IsAny<Guid>()), Times.Never);
            _mockBankAccountRepository.Verify(x => x.UpdateAsync(existingBankAccount), Times.Once);
        }

        /// <summary>
        /// Additional Test: Update with null RoutingNumber should work
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_NullRoutingNumber_ShouldUpdateSuccessfully()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Updated Name",
                RoutingNumber = null,
                SetAsPrimary = false
            };

            var existingBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = accountId,
                UserId = customerId,
                BankName = "Old Bank",
                AccountNumber = "9999999999",
                AccountHolderName = "Old Name",
                RoutingNumber = "123456789",
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.GetByIdAndUserAsync(accountId, customerId))
                .ReturnsAsync(existingBankAccount);

            _mockBankAccountRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .ReturnsAsync(true);

            // Act
            var result = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, updateDto);

            // Assert
            Assert.True(result);
            Assert.Null(existingBankAccount.RoutingNumber);
        }

        #endregion
    }
}

