using BusinessObject.DTOs.BankAccounts;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProviderBankServices
{
    public interface IProviderBankService
    {
        Task<IEnumerable<BankAccount>> GetBankAccounts(Guid providerId);
        Task<BankAccount?> GetBankAccountById(Guid id);
        Task AddBankAccount(Guid providerId, BankAccountCreateDto dto);
        Task<bool> UpdateBankAccount(Guid providerId, BankAccountUpdateDto dto);
    }
}
