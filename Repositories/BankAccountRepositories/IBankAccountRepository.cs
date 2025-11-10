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
        Task<BankAccount?> GetPrimaryAccountByUserAsync(Guid userId);
        Task<IEnumerable<BankAccount>> GetAllByUserIdAsync(Guid userId);
        Task<bool> HasMultiplePrimaryAccounts(Guid userId);
        Task<List<BankAccount>> GetBankAccountsByUserAsync(Guid userId);
        Task RemovePrimaryStatusAsync(Guid userId);
        Task<BankAccount?> GetByIdAndUserAsync(Guid accountId, Guid userId);
    }
}
