using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Models;
using Moq;
using Repositories.BankAccountRepositories;
using Services.CustomerBankServices;
using AutoMapper;

namespace Services.Tests.BankAccountTests
{
    /// <summary>
    /// Unit tests for CustomerBankService.CreateBankAccountAsync method
    /// Tests the Add Bank Account functionality
    /// </summary>
    public class CustomerBankServiceTests_AddBankAccount
    {
        private readonly Mock<IBankAccountRepository> _mockBankAccountRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CustomerBankService _customerBankService;

        public CustomerBankServiceTests_AddBankAccount()
        {
            _mockBankAccountRepository = new Mock<IBankAccountRepository>();
            _mockMapper = new Mock<IMapper>();
            _customerBankService = new CustomerBankService(_mockBankAccountRepository.Object, _mockMapper.Object);
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Create bank account with SetAsPrimary = TRUE
        /// Expected: Bank account created successfully with IsPrimary = true, and RemovePrimaryStatusAsync is called
        /// Backend behavior:
        ///   - Service returns BankAccountDto
        ///   - Controller returns 201 Created with the created bank account
        /// Note: "Bank account created successfully!" is a frontend message, not verified here.
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateBankAccount_SetAsPrimaryTrue_ShouldCreateAndRemoveOtherPrimaryStatus()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "John Doe",
                RoutingNumber = "123456789",
                SetAsPrimary = true
            };

            var expectedBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = createDto.RoutingNumber,
                IsPrimary = true
            };

            var expectedDto = new BankAccountDto
            {
                Id = expectedBankAccount.Id,
                BankName = expectedBankAccount.BankName,
                AccountNumber = expectedBankAccount.AccountNumber,
                AccountHolderName = expectedBankAccount.AccountHolderName,
                RoutingNumber = expectedBankAccount.RoutingNumber,
                IsPrimary = true
            };

            _mockBankAccountRepository.Setup(x => x.RemovePrimaryStatusAsync(customerId))
                .Returns(Task.CompletedTask);

            _mockBankAccountRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<BankAccountDto>(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(expectedDto);

            // Act
            var result = await _customerBankService.CreateBankAccountAsync(customerId, createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedDto.BankName, result.BankName);
            Assert.Equal(expectedDto.AccountNumber, result.AccountNumber);
            Assert.Equal(expectedDto.AccountHolderName, result.AccountHolderName);
            Assert.Equal(expectedDto.RoutingNumber, result.RoutingNumber);
            Assert.True(result.IsPrimary);

            // Verify RemovePrimaryStatusAsync was called because SetAsPrimary = true
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(customerId), Times.Once);
            _mockBankAccountRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.BankAccount>(
                ba => ba.UserId == customerId &&
                      ba.BankName == createDto.BankName &&
                      ba.AccountNumber == createDto.AccountNumber &&
                      ba.AccountHolderName == createDto.AccountHolderName &&
                      ba.IsPrimary == true
            )), Times.Once);
        }

        /// <summary>
        /// UTCID02: Create bank account with SetAsPrimary = FALSE
        /// Expected: Bank account created successfully with IsPrimary = false, and RemovePrimaryStatusAsync is NOT called
        /// Backend behavior:
        ///   - Service returns BankAccountDto
        ///   - Controller returns 201 Created with the created bank account
        /// Note: "Bank account created successfully!" is a frontend message, not verified here.
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateBankAccount_SetAsPrimaryFalse_ShouldCreateWithoutRemovingOtherPrimaryStatus()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "9876543210",
                AccountHolderName = "Jane Smith",
                RoutingNumber = "987654321",
                SetAsPrimary = false
            };

            var expectedBankAccount = new BusinessObject.Models.BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = createDto.RoutingNumber,
                IsPrimary = false
            };

            var expectedDto = new BankAccountDto
            {
                Id = expectedBankAccount.Id,
                BankName = expectedBankAccount.BankName,
                AccountNumber = expectedBankAccount.AccountNumber,
                AccountHolderName = expectedBankAccount.AccountHolderName,
                RoutingNumber = expectedBankAccount.RoutingNumber,
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<BankAccountDto>(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(expectedDto);

            // Act
            var result = await _customerBankService.CreateBankAccountAsync(customerId, createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedDto.BankName, result.BankName);
            Assert.Equal(expectedDto.AccountNumber, result.AccountNumber);
            Assert.Equal(expectedDto.AccountHolderName, result.AccountHolderName);
            Assert.Equal(expectedDto.RoutingNumber, result.RoutingNumber);
            Assert.False(result.IsPrimary);

            // Verify RemovePrimaryStatusAsync was NOT called because SetAsPrimary = false
            _mockBankAccountRepository.Verify(x => x.RemovePrimaryStatusAsync(It.IsAny<Guid>()), Times.Never);
            _mockBankAccountRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.BankAccount>(
                ba => ba.UserId == customerId &&
                      ba.BankName == createDto.BankName &&
                      ba.AccountNumber == createDto.AccountNumber &&
                      ba.AccountHolderName == createDto.AccountHolderName &&
                      ba.IsPrimary == false
            )), Times.Once);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Create bank account without RoutingNumber (optional field)
        /// </summary>
        [Fact]
        public async Task Additional_CreateBankAccount_WithoutRoutingNumber_ShouldCreateSuccessfully()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1111222233",
                AccountHolderName = "Test User",
                RoutingNumber = null,
                SetAsPrimary = false
            };

            var expectedDto = new BankAccountDto
            {
                Id = Guid.NewGuid(),
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = null,
                IsPrimary = false
            };

            _mockBankAccountRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<BankAccountDto>(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns(expectedDto);

            // Act
            var result = await _customerBankService.CreateBankAccountAsync(customerId, createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.RoutingNumber);
            _mockBankAccountRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.BankAccount>()), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify that each created bank account gets a unique ID
        /// </summary>
        [Fact]
        public async Task Additional_CreateBankAccount_ShouldGenerateUniqueId()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "John Doe",
                SetAsPrimary = false
            };

            BusinessObject.Models.BankAccount? capturedBankAccount = null;

            _mockBankAccountRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Callback<BusinessObject.Models.BankAccount>(ba => capturedBankAccount = ba)
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<BankAccountDto>(It.IsAny<BusinessObject.Models.BankAccount>()))
                .Returns((BusinessObject.Models.BankAccount ba) => new BankAccountDto
                {
                    Id = ba.Id,
                    BankName = ba.BankName,
                    AccountNumber = ba.AccountNumber,
                    AccountHolderName = ba.AccountHolderName,
                    IsPrimary = ba.IsPrimary
                });

            // Act
            var result = await _customerBankService.CreateBankAccountAsync(customerId, createDto);

            // Assert
            Assert.NotNull(capturedBankAccount);
            Assert.NotEqual(Guid.Empty, capturedBankAccount.Id);
            Assert.Equal(capturedBankAccount.Id, result.Id);
        }

        #endregion
    }
}

