using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.TransactionsDto;
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
        private readonly IMapper _mapper;

        public TransactionRepository(ShareItDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<TransactionSummaryDto>> GetTransactionsByProviderAsync(Guid providerId)
        {
            return await _context.Transactions
                .Where(t => t.Orders.Any(o => o.ProviderId == providerId) && t.Status == TransactionStatus.completed)
                .OrderByDescending(t => t.TransactionDate)
                .ProjectTo<TransactionSummaryDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalReceivedByProviderAsync(Guid providerId)
        {
            return await _context.Orders
                .Where(o => o.ProviderId == providerId &&
                             o.Transactions.Any(t => t.Status == TransactionStatus.completed))
                .SumAsync(o => o.TotalAmount);
        }

        public async Task<IEnumerable<ProviderPaymentDto>> GetAllProviderPaymentsAsync()
        {
            return await _context.Orders
                .Where(o => o.Transactions.Any(t => t.Status == TransactionStatus.completed))
                .GroupBy(o => new { o.ProviderId, o.Provider.Profile.FullName, o.Provider.Email })
                .Select(g => new ProviderPaymentDto
                {
                    ProviderId = g.Key.ProviderId,
                    ProviderName = g.Key.FullName ?? "Unknown",
                    ProviderEmail = g.Key.Email,
                    TotalEarned = g.Sum(o => o.TotalAmount),
                    CompletedOrders = g.Count(),
                    LastPayment = g.SelectMany(o => o.Transactions)
                                   .Where(t => t.Status == TransactionStatus.completed)
                                   .Max(t => (DateTime?)t.TransactionDate)
                })
                .OrderByDescending(p => p.TotalEarned)
                .ToListAsync();
        }

        public async Task<AllProvidersPaymentSummaryDto> GetAllProviderPaymentsSummaryAsync()
        {
            var providers = await GetAllProviderPaymentsAsync();
            
            // Get bank account info for each provider
            var providersWithBankInfo = new List<ProviderPaymentDto>();
            
            foreach (var provider in providers)
            {
                var bankAccount = await _context.BankAccounts
                    .Where(b => b.UserId == provider.ProviderId && b.IsPrimary)
                    .FirstOrDefaultAsync();
                
                provider.BankAccount = bankAccount?.AccountNumber;
                provider.BankName = bankAccount?.BankName;
                
                providersWithBankInfo.Add(provider);
            }

            return new AllProvidersPaymentSummaryDto
            {
                TotalAmountOwed = providersWithBankInfo.Sum(p => p.TotalEarned),
                TotalProviders = providersWithBankInfo.Count(),
                Providers = providersWithBankInfo
            };
        }

        public async Task<decimal> GetTotalPayoutsByUserAsync(Guid userId)
        {
            return await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content == "payout")
                .SumAsync(t => t.Amount);
        }

        public async Task<List<Transaction>> GetPayoutHistoryAsync(Guid userId, int page, int pageSize)
        {
            return await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content.StartsWith("Thanh toán"))
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetRecentPayoutsAsync(Guid userId, int count)
        {
            return await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content == "payout")
                .OrderByDescending(t => t.TransactionDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
    }
}
