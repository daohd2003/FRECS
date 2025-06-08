using BusinessObject.DTOs.BankAccounts;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProviderBankServices
{
    public class ProviderBankService : IProviderBankService
    {
        private readonly IBankAccountRepository _repo;

        public ProviderBankService(IBankAccountRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<BankAccount>> GetBankAccounts(Guid providerId)
        {
            return await _repo.GetAllByProviderIdAsync(providerId);
        }

        public async Task<BankAccount?> GetBankAccountById(Guid id)
        {
            return await _repo.GetByIdAsync(id);
        }

        public async Task AddBankAccount(BankAccountDto dto)
        {
            if (dto.IsPrimary)
            {
                bool hasMultiplePrimary = await _repo.HasMultiplePrimaryAccounts(dto.ProviderId);
                if (hasMultiplePrimary)
                {
                    throw new InvalidOperationException("Each provider can only have one primary bank account.");
                }
            }

            var entity = new BankAccount
            {
                Id = Guid.NewGuid(),
                ProviderId = dto.ProviderId,
                BankName = dto.BankName,
                AccountNumber = dto.AccountNumber,
                RoutingNumber = dto.RoutingNumber,
                IsPrimary = dto.IsPrimary
            };
            await _repo.AddAsync(entity);
        }

        public async Task<bool> UpdateBankAccount(BankAccountDto dto)
        {
            var existing = await _repo.GetByIdAsync(dto.Id);
            if (existing == null) return false;

            existing.BankName = dto.BankName;
            existing.AccountNumber = dto.AccountNumber;
            existing.RoutingNumber = dto.RoutingNumber;
            existing.IsPrimary = dto.IsPrimary;

            return await _repo.UpdateAsync(existing);
        }
    }
}
