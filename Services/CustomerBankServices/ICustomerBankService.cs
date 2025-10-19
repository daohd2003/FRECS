using BusinessObject.DTOs.RevenueDtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.CustomerBankServices
{
    public interface ICustomerBankService
    {
        Task<List<BankAccountDto>> GetBankAccountsAsync(Guid customerId);
        Task<BankAccountDto> CreateBankAccountAsync(Guid customerId, CreateBankAccountDto dto);
        Task<bool> UpdateBankAccountAsync(Guid customerId, Guid accountId, CreateBankAccountDto dto);
        Task<bool> DeleteBankAccountAsync(Guid customerId, Guid accountId);
        Task<bool> SetPrimaryBankAccountAsync(Guid customerId, Guid accountId);
    }
}
