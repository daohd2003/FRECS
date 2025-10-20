using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.WithdrawalRepositories
{
    public class WithdrawalRepository : Repository<WithdrawalRequest>, IWithdrawalRepository
    {
        public WithdrawalRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<WithdrawalRequest>> GetByProviderIdAsync(Guid providerId)
        {
            return await _context.WithdrawalRequests
                .Include(w => w.BankAccount)
                .Include(w => w.ProcessedByAdmin)
                .Where(w => w.ProviderId == providerId)
                .OrderByDescending(w => w.RequestDate)
                .ToListAsync();
        }

        public async Task<WithdrawalRequest?> GetByIdWithDetailsAsync(Guid id)
        {
            return await _context.WithdrawalRequests
                .Include(w => w.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(w => w.BankAccount)
                .Include(w => w.ProcessedByAdmin)
                    .ThenInclude(a => a.Profile)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<IEnumerable<WithdrawalRequest>> GetPendingRequestsAsync()
        {
            return await _context.WithdrawalRequests
                .Include(w => w.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(w => w.BankAccount)
                .Where(w => w.Status == WithdrawalStatus.Initiated)
                .OrderBy(w => w.RequestDate)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalPendingAmountAsync(Guid providerId)
        {
            return await _context.WithdrawalRequests
                .Where(w => w.ProviderId == providerId && w.Status == WithdrawalStatus.Initiated)
                .SumAsync(w => w.Amount);
        }

        public async Task<decimal> GetTotalCompletedAmountAsync(Guid providerId)
        {
            return await _context.WithdrawalRequests
                .Where(w => w.ProviderId == providerId && w.Status == WithdrawalStatus.Completed)
                .SumAsync(w => w.Amount);
        }
    }
}

