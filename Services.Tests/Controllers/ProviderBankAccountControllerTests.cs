using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.BankAccounts;
using BusinessObject.Models;
using Common.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.ProviderBankServices;
using ShareItAPI.Controllers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for ProviderBankAccountController focused on provider role coverage:
    /// View, Create, Update, Delete bank accounts.
    /// </summary>
    public class ProviderBankAccountControllerTests
    {
        private readonly Mock<IProviderBankService> _serviceMock;
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly UserContextHelper _userHelper;
        private readonly ProviderBankAccountController _controller;

        public ProviderBankAccountControllerTests()
        {
            _serviceMock = new Mock<IProviderBankService>();
            _httpContextAccessor = new HttpContextAccessor();
            _userHelper = new UserContextHelper(_httpContextAccessor);
            _controller = new ProviderBankAccountController(_serviceMock.Object, _userHelper);
        }

        private void SetUserContext(Guid userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            _httpContextAccessor.HttpContext = httpContext;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task UTCID01_GetBankAccounts_AsProviderOwner_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");

            var accounts = new List<BankAccount>
            {
                new() { Id = Guid.NewGuid(), UserId = providerId, BankName = "VCB", AccountNumber = "12345678", AccountHolderName = "Provider A", IsPrimary = true },
                new() { Id = Guid.NewGuid(), UserId = providerId, BankName = "ACB", AccountNumber = "87654321", AccountHolderName = "Provider A", IsPrimary = false }
            };

            _serviceMock.Setup(s => s.GetBankAccounts(providerId)).ReturnsAsync(accounts);

            var result = await _controller.GetByProviderId(providerId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank accounts retrieved", response.Message);
            var returnedAccounts = Assert.IsAssignableFrom<IEnumerable<BankAccount>>(response.Data);
            Assert.Equal(2, returnedAccounts.Count());

            _serviceMock.Verify(s => s.GetBankAccounts(providerId), Times.Once);
        }

        [Fact]
        public async Task UTCID02_GetBankAccounts_AsAdmin_ShouldBypassOwnerCheck()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(Guid.NewGuid(), "admin");

            var accounts = new List<BankAccount>();
            _serviceMock.Setup(s => s.GetBankAccounts(providerId)).ReturnsAsync(accounts);

            var result = await _controller.GetByProviderId(providerId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank accounts retrieved", response.Message);
            Assert.IsAssignableFrom<IEnumerable<BankAccount>>(response.Data);

            _serviceMock.Verify(s => s.GetBankAccounts(providerId), Times.Once);
        }

        [Fact]
        public async Task UTCID03_GetBankAccounts_NotOwnerProvider_ShouldThrow()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(Guid.NewGuid(), "provider");

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetByProviderId(providerId));

            _serviceMock.Verify(s => s.GetBankAccounts(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID04_GetBankAccountDetail_AsOwner_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            var accountId = Guid.NewGuid();
            var account = new BankAccount
            {
                Id = accountId,
                UserId = providerId,
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A"
            };

            _serviceMock.Setup(s => s.GetBankAccountById(accountId)).ReturnsAsync(account);

            var result = await _controller.GetById(accountId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank account found", response.Message);
            var returnedAccount = Assert.IsType<BankAccount>(response.Data);
            Assert.Equal(accountId, returnedAccount.Id);
        }

        [Fact]
        public async Task UTCID05_GetBankAccountDetail_NotFound_ShouldReturn404()
        {
            var accountId = Guid.NewGuid();
            SetUserContext(Guid.NewGuid(), "provider");
            _serviceMock.Setup(s => s.GetBankAccountById(accountId)).ReturnsAsync((BankAccount?)null);

            var result = await _controller.GetById(accountId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFound.Value);
            Assert.Equal("Bank account not found", response.Message);
            Assert.Null(response.Data);
        }

        [Fact]
        public async Task UTCID06_GetBankAccountDetail_NotOwnerProvider_ShouldThrow()
        {
            var providerId = Guid.NewGuid();
            var differentOwner = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = differentOwner,
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider B"
            };

            _serviceMock.Setup(s => s.GetBankAccountById(account.Id)).ReturnsAsync(account);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetById(account.Id));
        }

        [Fact]
        public async Task UTCID07_CreateBankAccount_ValidProvider_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            var dto = new BankAccountCreateDto
            {
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                RoutingNumber = "123456789",
                IsPrimary = true
            };

            var result = await _controller.Create(dto);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank account added", response.Message);
            Assert.Null(response.Data);

            _serviceMock.Verify(s => s.AddBankAccount(providerId, dto), Times.Once);
        }

        [Fact]
        public async Task UTCID08_CreateBankAccount_InvalidModel_ShouldReturn400()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            _controller.ModelState.AddModelError("BankName", "Bank name is required.");
            var dto = new BankAccountCreateDto();

            var result = await _controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Contains("Bank name is required.", response.Message);

            _serviceMock.Verify(s => s.AddBankAccount(It.IsAny<Guid>(), It.IsAny<BankAccountCreateDto>()), Times.Never);
        }

        [Fact]
        public async Task UTCID09_UpdateBankAccount_Success_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            var dto = new BankAccountUpdateDto
            {
                Id = Guid.NewGuid(),
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                RoutingNumber = "1111",
                IsPrimary = false
            };
            _serviceMock.Setup(s => s.UpdateBankAccount(providerId, dto)).ReturnsAsync(true);

            var result = await _controller.Update(dto);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank account updated", response.Message);

            _serviceMock.Verify(s => s.UpdateBankAccount(providerId, dto), Times.Once);
        }

        [Fact]
        public async Task UTCID10_UpdateBankAccount_NotFound_ShouldReturn404()
        {
            var providerId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            var dto = new BankAccountUpdateDto
            {
                Id = Guid.NewGuid(),
                BankName = "ACB",
                AccountNumber = "99999999",
                AccountHolderName = "Provider B",
                IsPrimary = false
            };
            _serviceMock.Setup(s => s.UpdateBankAccount(providerId, dto)).ReturnsAsync(false);

            var result = await _controller.Update(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFound.Value);
            Assert.Equal("Bank account not found", response.Message);
        }

        [Fact]
        public async Task UTCID11_UpdateBankAccount_InvalidModel_ShouldReturn400()
        {
            _controller.ModelState.AddModelError("AccountNumber", "Account number is invalid.");
            var dto = new BankAccountUpdateDto();

            var result = await _controller.Update(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Contains("Account number is invalid.", response.Message);

            _serviceMock.Verify(s => s.UpdateBankAccount(It.IsAny<Guid>(), It.IsAny<BankAccountUpdateDto>()), Times.Never);
        }

        [Fact]
        public async Task UTCID12_DeleteBankAccount_Success_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            _serviceMock.Setup(s => s.DeleteBankAccount(providerId, accountId)).ReturnsAsync(true);

            var result = await _controller.Delete(accountId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Bank account has been deleted.", response.Message);

            _serviceMock.Verify(s => s.DeleteBankAccount(providerId, accountId), Times.Once);
        }

        [Fact]
        public async Task UTCID13_DeleteBankAccount_NotFound_ShouldReturn404()
        {
            var providerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            SetUserContext(providerId, "provider");
            _serviceMock.Setup(s => s.DeleteBankAccount(providerId, accountId)).ReturnsAsync(false);

            var result = await _controller.Delete(accountId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFound.Value);
            Assert.Equal("Bank account not found or you don't have permission to delete it", response.Message);
        }
    }
}

