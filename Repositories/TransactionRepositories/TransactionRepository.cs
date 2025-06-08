using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.TransactionRepositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ShareItDbContext _context;

        public TransactionRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByProviderAsync(Guid providerId)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                .Where(t => t.ProviderId == providerId && t.Status == TransactionStatus.completed)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalReceivedByProviderAsync(Guid providerId)
        {
            return await _context.Transactions
                .Where(t => t.ProviderId == providerId && t.Status == TransactionStatus.completed)
                .SumAsync(t => t.Amount);
        }
    }
}
