using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.BankAccountRepositories
{
    public interface IBankAccountRepository : IRepository<BankAccount>
    {
        Task<BankAccount?> GetPrimaryAccountByProviderAsync(Guid providerId);
        Task<IEnumerable<BankAccount>> GetAllByProviderIdAsync(Guid providerId);
        Task<bool> HasMultiplePrimaryAccounts(Guid providerId);
        Task<List<BankAccount>> GetBankAccountsWithProviderAsync(Guid providerId);
        Task RemovePrimaryStatusAsync(Guid providerId);
        Task<BankAccount?> GetByIdAndProviderAsync(Guid accountId, Guid providerId);
    }
}
