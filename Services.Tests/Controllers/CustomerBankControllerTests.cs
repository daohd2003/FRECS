using BusinessObject.DTOs.RevenueDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Services.CustomerBankServices;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for Customer Bank Account Controller (API Layer)
    /// Verifies API responses and HTTP status codes
    /// 
    /// Test Coverage:
    /// - Get Bank Accounts (GET /api/customer/banks)
    /// 
    /// Note: Controller returns List<BankAccountDto> directly (no ApiResponse wrapper)
    ///       Frontend message "No Refund Accounts" is NOT tested (FE only)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~CustomerBankControllerTests"
    /// </summary>
    public class CustomerBankControllerTests
    {
        private readonly Mock<ICustomerBankService> _mockCustomerBankService;
        private readonly Mock<ILogger<CustomerBankController>> _mockLogger;
        private readonly CustomerBankController _controller;

        public CustomerBankControllerTests()
        {
            _mockCustomerBankService = new Mock<ICustomerBankService>();
            _mockLogger = new Mock<ILogger<CustomerBankController>>();
            _controller = new CustomerBankController(_mockCustomerBankService.Object, _mockLogger.Object);
        }

        private void SetupUserContext(Guid userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region GetBankAccounts Tests

        /// <summary>
        /// UTCID01: Has multiple accounts, one of which is primary
        /// Expected: 200 OK with list containing primary flag
        /// Note: No ApiResponse wrapper, returns List<BankAccountDto> directly
        /// </summary>
        [Fact]
        public async Task UTCID01_GetBankAccounts_MultipleAccountsWithPrimary_ShouldReturn200WithList()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var bankAccounts = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    RoutingNumber = "VCB123",
                    IsPrimary = true, // Primary account
                    CreatedAt = DateTime.UtcNow
                },
                new BankAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    RoutingNumber = "TCB456",
                    IsPrimary = false, // Not primary
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockCustomerBankService.Setup(x => x.GetBankAccountsAsync(customerId))
                .ReturnsAsync(bankAccounts);

            // Act
            var result = await _controller.GetBankAccounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);

            var returnedAccounts = Assert.IsType<List<BankAccountDto>>(okResult.Value);
            Assert.Equal(2, returnedAccounts.Count);
            Assert.Single(returnedAccounts.Where(a => a.IsPrimary)); // Only one primary
            Assert.Equal("Vietcombank", returnedAccounts.First(a => a.IsPrimary).BankName);

            _mockCustomerBankService.Verify(x => x.GetBankAccountsAsync(customerId), Times.Once);
        }

        /// <summary>
        /// UTCID02: Has multiple accounts, none are primary
        /// Expected: 200 OK with list without primary flag
        /// </summary>
        [Fact]
        public async Task UTCID02_GetBankAccounts_MultipleAccountsNoPrimary_ShouldReturn200WithList()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var bankAccounts = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    IsPrimary = false, // Not primary
                    CreatedAt = DateTime.UtcNow
                },
                new BankAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Techcombank",
                    AccountNumber = "0987654321",
                    AccountHolderName = "Test User",
                    IsPrimary = false, // Not primary
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockCustomerBankService.Setup(x => x.GetBankAccountsAsync(customerId))
                .ReturnsAsync(bankAccounts);

            // Act
            var result = await _controller.GetBankAccounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);

            var returnedAccounts = Assert.IsType<List<BankAccountDto>>(okResult.Value);
            Assert.Equal(2, returnedAccounts.Count);
            Assert.Empty(returnedAccounts.Where(a => a.IsPrimary)); // No primary accounts
            Assert.All(returnedAccounts, a => Assert.False(a.IsPrimary));

            _mockCustomerBankService.Verify(x => x.GetBankAccountsAsync(customerId), Times.Once);
        }

        /// <summary>
        /// UTCID03: Has no bank accounts
        /// Expected: 200 OK with empty list
        /// Frontend Message: "No Refund Accounts" (FE only - NOT verified here)
        /// </summary>
        [Fact]
        public async Task UTCID03_GetBankAccounts_NoAccounts_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var emptyBankAccounts = new List<BankAccountDto>(); // Empty list

            _mockCustomerBankService.Setup(x => x.GetBankAccountsAsync(customerId))
                .ReturnsAsync(emptyBankAccounts);

            // Act
            var result = await _controller.GetBankAccounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);

            var returnedAccounts = Assert.IsType<List<BankAccountDto>>(okResult.Value);
            Assert.Empty(returnedAccounts); // Empty list

            _mockCustomerBankService.Verify(x => x.GetBankAccountsAsync(customerId), Times.Once);

            // Controller returns 200 OK with empty list
            // Frontend displays: "No Refund Accounts" (FE message, not from API)
        }

        /// <summary>
        /// Additional Test: Unauthorized if customer ID is empty/missing
        /// </summary>
        [Fact]
        public async Task Additional_GetBankAccounts_EmptyCustomerId_ShouldReturn401Unauthorized()
        {
            // Arrange
            // Setup empty user context (no claims)
            var claims = new List<Claim>(); // No NameIdentifier claim
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.GetBankAccounts();

            // Assert
            // Controller returns Unauthorized("Customer not found")
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
            Assert.Equal("Customer not found", unauthorizedResult.Value);

            _mockCustomerBankService.Verify(x => x.GetBankAccountsAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Verify Last4Digits computed property
        /// </summary>
        [Fact]
        public async Task Additional_GetBankAccounts_ShouldReturnLast4Digits()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var bankAccounts = new List<BankAccountDto>
            {
                new BankAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Vietcombank",
                    AccountNumber = "1234567890",
                    AccountHolderName = "Test User",
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockCustomerBankService.Setup(x => x.GetBankAccountsAsync(customerId))
                .ReturnsAsync(bankAccounts);

            // Act
            var result = await _controller.GetBankAccounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedAccounts = Assert.IsType<List<BankAccountDto>>(okResult.Value);
            
            Assert.Single(returnedAccounts);
            Assert.Equal("7890", returnedAccounts.First().Last4Digits); // Last 4 digits
        }

        #endregion

        #region CreateBankAccount Tests

        /// <summary>
        /// UTCID01: Create bank account with SetAsPrimary = TRUE
        /// Expected: 201 Created with the created bank account
        /// Backend behavior:
        ///   - Controller validates ModelState
        ///   - Service creates bank account and removes primary status from other accounts
        ///   - Controller returns 201 Created
        /// Note: "Bank account created successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateBankAccount_SetAsPrimaryTrue_ShouldReturn201Created()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var createDto = new CreateBankAccountDto
            {
                BankName = "Vietcombank",
                AccountNumber = "1234567890",
                AccountHolderName = "John Doe",
                RoutingNumber = "123456789",
                SetAsPrimary = true
            };

            var createdAccount = new BankAccountDto
            {
                Id = Guid.NewGuid(),
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = createDto.RoutingNumber,
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockCustomerBankService.Setup(x => x.CreateBankAccountAsync(customerId, createDto))
                .ReturnsAsync(createdAccount);

            // Act
            var result = await _controller.CreateBankAccount(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(nameof(CustomerBankController.GetBankAccounts), createdResult.ActionName);

            var returnedAccount = Assert.IsType<BankAccountDto>(createdResult.Value);
            Assert.Equal(createdAccount.Id, returnedAccount.Id);
            Assert.Equal(createdAccount.BankName, returnedAccount.BankName);
            Assert.Equal(createdAccount.AccountNumber, returnedAccount.AccountNumber);
            Assert.Equal(createdAccount.AccountHolderName, returnedAccount.AccountHolderName);
            Assert.True(returnedAccount.IsPrimary);

            _mockCustomerBankService.Verify(x => x.CreateBankAccountAsync(customerId, createDto), Times.Once);

            // Note: "Bank account created successfully!" is a frontend message.
            // The controller does not return this message; it only returns 201 Created with the account data.
        }

        /// <summary>
        /// UTCID02: Create bank account with SetAsPrimary = FALSE
        /// Expected: 201 Created with the created bank account
        /// Backend behavior:
        ///   - Controller validates ModelState
        ///   - Service creates bank account without removing primary status from other accounts
        ///   - Controller returns 201 Created
        /// Note: "Bank account created successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateBankAccount_SetAsPrimaryFalse_ShouldReturn201Created()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var createDto = new CreateBankAccountDto
            {
                BankName = "ACB Bank",
                AccountNumber = "9876543210",
                AccountHolderName = "Jane Smith",
                RoutingNumber = "987654321",
                SetAsPrimary = false
            };

            var createdAccount = new BankAccountDto
            {
                Id = Guid.NewGuid(),
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = createDto.RoutingNumber,
                IsPrimary = false,
                CreatedAt = DateTime.UtcNow
            };

            _mockCustomerBankService.Setup(x => x.CreateBankAccountAsync(customerId, createDto))
                .ReturnsAsync(createdAccount);

            // Act
            var result = await _controller.CreateBankAccount(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(nameof(CustomerBankController.GetBankAccounts), createdResult.ActionName);

            var returnedAccount = Assert.IsType<BankAccountDto>(createdResult.Value);
            Assert.Equal(createdAccount.Id, returnedAccount.Id);
            Assert.Equal(createdAccount.BankName, returnedAccount.BankName);
            Assert.Equal(createdAccount.AccountNumber, returnedAccount.AccountNumber);
            Assert.Equal(createdAccount.AccountHolderName, returnedAccount.AccountHolderName);
            Assert.False(returnedAccount.IsPrimary);

            _mockCustomerBankService.Verify(x => x.CreateBankAccountAsync(customerId, createDto), Times.Once);

            // Note: "Bank account created successfully!" is a frontend message.
            // The controller does not return this message; it only returns 201 Created with the account data.
        }

        /// <summary>
        /// Additional Test: Create bank account with empty customer ID should return 401
        /// </summary>
        [Fact]
        public async Task Additional_CreateBankAccount_EmptyCustomerId_ShouldReturn401Unauthorized()
        {
            // Arrange
            // Setup empty user context (no claims)
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            // Act
            var result = await _controller.CreateBankAccount(createDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
            Assert.Equal("Customer not found", unauthorizedResult.Value);

            _mockCustomerBankService.Verify(x => x.CreateBankAccountAsync(It.IsAny<Guid>(), It.IsAny<CreateBankAccountDto>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Create bank account without RoutingNumber (optional field)
        /// </summary>
        [Fact]
        public async Task Additional_CreateBankAccount_WithoutRoutingNumber_ShouldReturn201Created()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1111222233",
                AccountHolderName = "Test User",
                RoutingNumber = null,
                SetAsPrimary = false
            };

            var createdAccount = new BankAccountDto
            {
                Id = Guid.NewGuid(),
                BankName = createDto.BankName,
                AccountNumber = createDto.AccountNumber,
                AccountHolderName = createDto.AccountHolderName,
                RoutingNumber = null,
                IsPrimary = false,
                CreatedAt = DateTime.UtcNow
            };

            _mockCustomerBankService.Setup(x => x.CreateBankAccountAsync(customerId, createDto))
                .ReturnsAsync(createdAccount);

            // Act
            var result = await _controller.CreateBankAccount(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);

            var returnedAccount = Assert.IsType<BankAccountDto>(createdResult.Value);
            Assert.Null(returnedAccount.RoutingNumber);
        }

        /// <summary>
        /// Additional Test: Service exception should return 500 Internal Server Error
        /// </summary>
        [Fact]
        public async Task Additional_CreateBankAccount_ServiceException_ShouldReturn500()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            SetupUserContext(customerId);

            var createDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            _mockCustomerBankService.Setup(x => x.CreateBankAccountAsync(customerId, createDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreateBankAccount(createDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error", objectResult.Value);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        #endregion

        #region UpdateBankAccount Tests

        /// <summary>
        /// UTCID01: Update bank account with SetAsPrimary = TRUE
        /// Expected: 200 OK
        /// Backend behavior:
        ///   - Controller validates ModelState
        ///   - Service updates bank account and removes primary status from other accounts
        ///   - Controller returns 200 OK
        /// Note: "Bank account updated successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID01_UpdateBankAccount_SetAsPrimaryTrue_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated Vietcombank",
                AccountNumber = "9999888877",
                AccountHolderName = "Updated John Doe",
                RoutingNumber = "999888777",
                SetAsPrimary = true
            };

            _mockCustomerBankService.Setup(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _mockCustomerBankService.Verify(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto), Times.Once);

            // Note: "Bank account updated successfully!" is a frontend message.
            // The controller does not return this message; it only returns 200 OK.
        }

        /// <summary>
        /// UTCID02: Update bank account with SetAsPrimary = FALSE
        /// Expected: 200 OK
        /// Backend behavior:
        ///   - Controller validates ModelState
        ///   - Service updates bank account without removing primary status from other accounts
        ///   - Controller returns 200 OK
        /// Note: "Bank account updated successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID02_UpdateBankAccount_SetAsPrimaryFalse_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Updated ACB Bank",
                AccountNumber = "8888777766",
                AccountHolderName = "Updated Jane Smith",
                RoutingNumber = "888777666",
                SetAsPrimary = false
            };

            _mockCustomerBankService.Setup(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _mockCustomerBankService.Verify(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto), Times.Once);

            // Note: "Bank account updated successfully!" is a frontend message.
            // The controller does not return this message; it only returns 200 OK.
        }

        /// <summary>
        /// Additional Test: Update non-existent bank account should return 404 Not Found
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_NonExistentAccount_ShouldReturn404NotFound()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            _mockCustomerBankService.Setup(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto))
                .ReturnsAsync(false); // Service returns false when account not found

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Bank account not found", notFoundResult.Value);
        }

        /// <summary>
        /// Additional Test: Update bank account with empty customer ID should return 401
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_EmptyCustomerId_ShouldReturn401Unauthorized()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            
            // Setup empty user context (no claims)
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
            Assert.Equal("Customer not found", unauthorizedResult.Value);

            _mockCustomerBankService.Verify(x => x.UpdateBankAccountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateBankAccountDto>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Service exception should return 500 Internal Server Error
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_ServiceException_ShouldReturn500()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                SetAsPrimary = false
            };

            _mockCustomerBankService.Setup(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error", objectResult.Value);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        /// <summary>
        /// Additional Test: Update with null RoutingNumber should work
        /// </summary>
        [Fact]
        public async Task Additional_UpdateBankAccount_NullRoutingNumber_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            var updateDto = new CreateBankAccountDto
            {
                BankName = "Test Bank",
                AccountNumber = "1234567890",
                AccountHolderName = "Test User",
                RoutingNumber = null,
                SetAsPrimary = false
            };

            _mockCustomerBankService.Setup(x => x.UpdateBankAccountAsync(customerId, accountId, updateDto))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateBankAccount(accountId, updateDto);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        #endregion

        #region DeleteBankAccount Tests

        /// <summary>
        /// UTCID01: Delete existing bank account
        /// Expected: 200 OK
        /// Backend behavior:
        ///   - Controller gets customer ID from claims
        ///   - Service deletes bank account
        ///   - Controller returns 200 OK
        /// Note: "Bank account deleted successfully!" is a frontend message, not from API.
        /// </summary>
        [Fact]
        public async Task UTCID01_DeleteBankAccount_ExistingAccount_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            _mockCustomerBankService.Setup(x => x.DeleteBankAccountAsync(customerId, accountId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _mockCustomerBankService.Verify(x => x.DeleteBankAccountAsync(customerId, accountId), Times.Once);

            // Note: "Bank account deleted successfully!" is a frontend message.
            // The controller does not return this message; it only returns 200 OK.
        }

        /// <summary>
        /// Additional Test: Delete non-existent bank account should return 404 Not Found
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_NonExistentAccount_ShouldReturn404NotFound()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            _mockCustomerBankService.Setup(x => x.DeleteBankAccountAsync(customerId, accountId))
                .ReturnsAsync(false); // Service returns false when account not found

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Bank account not found", notFoundResult.Value);
        }

        /// <summary>
        /// Additional Test: Delete bank account with empty customer ID should return 401
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_EmptyCustomerId_ShouldReturn401Unauthorized()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            
            // Setup empty user context (no claims)
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
            Assert.Equal("Customer not found", unauthorizedResult.Value);

            _mockCustomerBankService.Verify(x => x.DeleteBankAccountAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Service exception should return 500 Internal Server Error
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_ServiceException_ShouldReturn500()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            _mockCustomerBankService.Setup(x => x.DeleteBankAccountAsync(customerId, accountId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error", objectResult.Value);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        /// <summary>
        /// Additional Test: Delete primary bank account should work
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_PrimaryAccount_ShouldReturn200OK()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            _mockCustomerBankService.Setup(x => x.DeleteBankAccountAsync(customerId, accountId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        /// <summary>
        /// Additional Test: Delete account belonging to different user should return 404
        /// </summary>
        [Fact]
        public async Task Additional_DeleteBankAccount_DifferentUser_ShouldReturn404NotFound()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetupUserContext(customerId);

            // Service will return false because account doesn't belong to this user
            _mockCustomerBankService.Setup(x => x.DeleteBankAccountAsync(customerId, accountId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteBankAccount(accountId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Bank account not found", notFoundResult.Value);
        }

        #endregion
    }
}

