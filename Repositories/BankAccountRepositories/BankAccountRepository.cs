using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.BankAccountRepositories
{
    public class BankAccountRepository : Repository<BankAccount>, IBankAccountRepository
    {
        public BankAccountRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<BankAccount?> GetPrimaryAccountByProviderAsync(Guid providerId)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(b => b.ProviderId == providerId && b.IsPrimary);
        }

        public async Task<IEnumerable<BankAccount>> GetAllByProviderIdAsync(Guid providerId)
        {
            return await _context.BankAccounts
                .Where(b => b.ProviderId == providerId)
                .ToListAsync();
        }

        public async Task<bool> HasMultiplePrimaryAccounts(Guid providerId)
        {
            var countPrimary = await _context.BankAccounts
                .Where(b => b.ProviderId == providerId && b.IsPrimary)
                .CountAsync();

            return countPrimary > 1;
        }

        public async Task<List<BankAccount>> GetBankAccountsWithProviderAsync(Guid providerId)
        {
            return await _context.BankAccounts
                .Include(ba => ba.Provider)
                .ThenInclude(p => p.Profile)
                .Where(ba => ba.ProviderId == providerId)
                .ToListAsync();
        }

        public async Task RemovePrimaryStatusAsync(Guid providerId)
        {
            await _context.BankAccounts
                .Where(ba => ba.ProviderId == providerId && ba.IsPrimary)
                .ExecuteUpdateAsync(ba => ba.SetProperty(b => b.IsPrimary, false));
        }

        public async Task<BankAccount?> GetByIdAndProviderAsync(Guid accountId, Guid providerId)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == accountId && ba.ProviderId == providerId);
        }
    }
}
