using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using Services.CustomerBankServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services.CustomerBankServices
{
    public class CustomerBankService : ICustomerBankService
    {
        private readonly IBankAccountRepository _bankAccountRepository;
        private readonly IMapper _mapper;

        public CustomerBankService(IBankAccountRepository bankAccountRepository, IMapper mapper)
        {
            _bankAccountRepository = bankAccountRepository;
            _mapper = mapper;
        }

        public async Task<List<BankAccountDto>> GetBankAccountsAsync(Guid customerId)
        {
            var bankAccounts = await _bankAccountRepository.GetBankAccountsByUserAsync(customerId);
            return _mapper.Map<List<BankAccountDto>>(bankAccounts);
        }

        public async Task<BankAccountDto> CreateBankAccountAsync(Guid customerId, CreateBankAccountDto dto)
        {
            // If setting as primary, remove primary status from other accounts
            if (dto.SetAsPrimary)
            {
                await _bankAccountRepository.RemovePrimaryStatusAsync(customerId);
            }

            var bankAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                BankName = dto.BankName,
                AccountNumber = dto.AccountNumber,
                AccountHolderName = dto.AccountHolderName,
                RoutingNumber = dto.RoutingNumber,
                IsPrimary = dto.SetAsPrimary
            };

            await _bankAccountRepository.AddAsync(bankAccount);

            return _mapper.Map<BankAccountDto>(bankAccount);
        }

        public async Task<bool> UpdateBankAccountAsync(Guid customerId, Guid accountId, CreateBankAccountDto dto)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, customerId);

            if (bankAccount == null) return false;

            bankAccount.BankName = dto.BankName;
            bankAccount.AccountNumber = dto.AccountNumber;
            bankAccount.AccountHolderName = dto.AccountHolderName;
            bankAccount.RoutingNumber = dto.RoutingNumber;

            if (dto.SetAsPrimary && !bankAccount.IsPrimary)
            {
                await _bankAccountRepository.RemovePrimaryStatusAsync(customerId);
                bankAccount.IsPrimary = true;
            }

            await _bankAccountRepository.UpdateAsync(bankAccount);
            return true;
        }

        public async Task<bool> DeleteBankAccountAsync(Guid customerId, Guid accountId)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, customerId);

            if (bankAccount == null) return false;

            await _bankAccountRepository.DeleteAsync(accountId);
            return true;
        }

        public async Task<bool> SetPrimaryBankAccountAsync(Guid customerId, Guid accountId)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, customerId);

            if (bankAccount == null) return false;

            await _bankAccountRepository.RemovePrimaryStatusAsync(customerId);

            bankAccount.IsPrimary = true;
            await _bankAccountRepository.UpdateAsync(bankAccount);
            return true;
        }
    }
}
