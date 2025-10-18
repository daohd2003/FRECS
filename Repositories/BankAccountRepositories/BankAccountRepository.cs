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

        public async Task<BankAccount?> GetPrimaryAccountByUserAsync(Guid userId)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(b => b.UserId == userId && b.IsPrimary);
        }

        public async Task<IEnumerable<BankAccount>> GetAllByUserIdAsync(Guid userId)
        {
            return await _context.BankAccounts
                .Where(b => b.UserId == userId)
                .ToListAsync();
        }

        public async Task<bool> HasMultiplePrimaryAccounts(Guid userId)
        {
            var countPrimary = await _context.BankAccounts
                .Where(b => b.UserId == userId && b.IsPrimary)
                .CountAsync();

            return countPrimary > 1;
        }

        public async Task<List<BankAccount>> GetBankAccountsByUserAsync(Guid userId)
        {
            return await _context.BankAccounts
                .Where(ba => ba.UserId == userId)
                .ToListAsync();
        }

        public async Task RemovePrimaryStatusAsync(Guid userId)
        {
            await _context.BankAccounts
                .Where(ba => ba.UserId == userId && ba.IsPrimary)
                .ExecuteUpdateAsync(ba => ba.SetProperty(b => b.IsPrimary, false));
        }

        public async Task<BankAccount?> GetByIdAndUserAsync(Guid accountId, Guid userId)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == accountId && ba.UserId == userId);
        }
    }
}
