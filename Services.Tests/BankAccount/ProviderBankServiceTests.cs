using BusinessObject.DTOs.BankAccounts;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using Services.ProviderBankServices;
using System.Collections.Generic;

namespace Services.Tests.BankAccountTests
{
    /// <summary>
    /// Unit tests for ProviderBankService covering provider role operations:
    /// view, create, update, delete bank accounts.
    /// </summary>
    public class ProviderBankServiceTests
    {
        private readonly Mock<IBankAccountRepository> _repoMock;
        private readonly ProviderBankService _service;

        public ProviderBankServiceTests()
        {
            _repoMock = new Mock<IBankAccountRepository>();
            _service = new ProviderBankService(_repoMock.Object);
        }

        [Fact]
        public async Task UTCID01_GetBankAccounts_ShouldReturnRepositoryData()
        {
            var providerId = Guid.NewGuid();
            var accounts = new List<BankAccount>
            {
                new() { Id = Guid.NewGuid(), UserId = providerId, BankName = "VCB" },
                new() { Id = Guid.NewGuid(), UserId = providerId, BankName = "ACB" }
            };

            _repoMock.Setup(r => r.GetAllByUserIdAsync(providerId)).ReturnsAsync(accounts);

            var result = await _service.GetBankAccounts(providerId);

            Assert.Equal(accounts, result);
            _repoMock.Verify(r => r.GetAllByUserIdAsync(providerId), Times.Once);
        }

        [Fact]
        public async Task UTCID02_GetBankAccountById_ShouldReturnEntity()
        {
            var accountId = Guid.NewGuid();
            var account = new BankAccount { Id = accountId, UserId = Guid.NewGuid(), BankName = "VCB" };
            _repoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(account);

            var result = await _service.GetBankAccountById(accountId);

            Assert.Equal(account, result);
            _repoMock.Verify(r => r.GetByIdAsync(accountId), Times.Once);
        }

        [Fact]
        public async Task UTCID03_AddBankAccount_Primary_WhenUnique_ShouldPersist()
        {
            var providerId = Guid.NewGuid();
            var dto = new BankAccountCreateDto
            {
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                RoutingNumber = "111111",
                IsPrimary = true
            };

            _repoMock.Setup(r => r.HasMultiplePrimaryAccounts(providerId)).ReturnsAsync(false);

            await _service.AddBankAccount(providerId, dto);

            _repoMock.Verify(r => r.HasMultiplePrimaryAccounts(providerId), Times.Once);
            _repoMock.Verify(r => r.AddAsync(It.Is<BankAccount>(b =>
                b.UserId == providerId &&
                b.BankName == dto.BankName &&
                b.AccountNumber == dto.AccountNumber &&
                b.AccountHolderName == dto.AccountHolderName &&
                b.RoutingNumber == dto.RoutingNumber &&
                b.IsPrimary == dto.IsPrimary &&
                b.Id != Guid.Empty)), Times.Once);
        }

        [Fact]
        public async Task UTCID04_AddBankAccount_PrimaryDuplicate_ShouldThrow()
        {
            var providerId = Guid.NewGuid();
            var dto = new BankAccountCreateDto
            {
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                IsPrimary = true
            };
            _repoMock.Setup(r => r.HasMultiplePrimaryAccounts(providerId)).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddBankAccount(providerId, dto));

            _repoMock.Verify(r => r.AddAsync(It.IsAny<BankAccount>()), Times.Never);
        }

        [Fact]
        public async Task UTCID05_UpdateBankAccount_Success_ShouldPersistChanges()
        {
            var providerId = Guid.NewGuid();
            var dto = new BankAccountUpdateDto
            {
                Id = Guid.NewGuid(),
                BankName = "Updated VCB",
                AccountNumber = "88888888",
                AccountHolderName = "Provider A",
                RoutingNumber = "222222",
                IsPrimary = false
            };

            var existing = new BankAccount
            {
                Id = dto.Id,
                UserId = providerId,
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                RoutingNumber = "111111",
                IsPrimary = false
            };

            _repoMock.Setup(r => r.GetByIdAsync(dto.Id)).ReturnsAsync(existing);
            _repoMock.Setup(r => r.UpdateAsync(existing)).ReturnsAsync(true);

            var result = await _service.UpdateBankAccount(providerId, dto);

            Assert.True(result);
            Assert.Equal(dto.BankName, existing.BankName);
            Assert.Equal(dto.AccountNumber, existing.AccountNumber);
            Assert.Equal(dto.AccountHolderName, existing.AccountHolderName);
            Assert.Equal(dto.RoutingNumber, existing.RoutingNumber);
            Assert.Equal(dto.IsPrimary, existing.IsPrimary);
            _repoMock.Verify(r => r.UpdateAsync(existing), Times.Once);
        }

        [Fact]
        public async Task UTCID06_UpdateBankAccount_NotOwner_ShouldReturnFalse()
        {
            var providerId = Guid.NewGuid();
            var dto = new BankAccountUpdateDto { Id = Guid.NewGuid(), BankName = "VCB" };
            var existing = new BankAccount { Id = dto.Id, UserId = Guid.NewGuid() };

            _repoMock.Setup(r => r.GetByIdAsync(dto.Id)).ReturnsAsync(existing);

            var result = await _service.UpdateBankAccount(providerId, dto);

            Assert.False(result);
            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<BankAccount>()), Times.Never);
        }

        [Fact]
        public async Task UTCID07_UpdateBankAccount_SetPrimaryWithExistingPrimary_ShouldThrow()
        {
            var providerId = Guid.NewGuid();
            var dto = new BankAccountUpdateDto
            {
                Id = Guid.NewGuid(),
                BankName = "VCB",
                AccountNumber = "12345678",
                AccountHolderName = "Provider A",
                IsPrimary = true
            };
            var existing = new BankAccount { Id = dto.Id, UserId = providerId, IsPrimary = false };

            _repoMock.Setup(r => r.GetByIdAsync(dto.Id)).ReturnsAsync(existing);
            _repoMock.Setup(r => r.HasMultiplePrimaryAccounts(providerId)).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateBankAccount(providerId, dto));

            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<BankAccount>()), Times.Never);
        }

        [Fact]
        public async Task UTCID08_DeleteBankAccount_Success_ShouldDelete()
        {
            var providerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var existing = new BankAccount { Id = accountId, UserId = providerId };

            _repoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(existing);
            _repoMock.Setup(r => r.DeleteAsync(accountId)).ReturnsAsync(true);

            var result = await _service.DeleteBankAccount(providerId, accountId);

            Assert.True(result);
            _repoMock.Verify(r => r.DeleteAsync(accountId), Times.Once);
        }

        [Fact]
        public async Task UTCID09_DeleteBankAccount_NotOwner_ShouldReturnFalse()
        {
            var providerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var existing = new BankAccount { Id = accountId, UserId = Guid.NewGuid() };

            _repoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(existing);

            var result = await _service.DeleteBankAccount(providerId, accountId);

            Assert.False(result);
            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }
    }
}

