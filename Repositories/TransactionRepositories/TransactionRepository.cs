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
            // Get all returned orders for this provider
            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.ProviderId == providerId && o.Status == OrderStatus.returned)
                .ToListAsync();

            decimal totalGrossRevenue = 0;
            decimal totalPlatformFee = 0;

            // Calculate net revenue (same logic as RevenueRepository.GetTotalEarningsAsync)
            foreach (var order in orders)
            {
                // Add gross revenue (Subtotal excludes deposit)
                totalGrossRevenue += order.Subtotal;

                // Calculate platform fee for each item
                foreach (var item in order.Items)
                {
                    decimal itemRevenue = 0;

                    if (item.TransactionType == TransactionType.rental)
                    {
                        itemRevenue = item.DailyRate * (item.RentalDays ?? 0) * item.Quantity;
                        totalPlatformFee += itemRevenue * 0.20m; // 20% commission
                    }
                    else if (item.TransactionType == TransactionType.purchase)
                    {
                        itemRevenue = item.DailyRate * item.Quantity;
                        totalPlatformFee += itemRevenue * 0.10m; // 10% commission
                    }
                }
            }

            // Net earnings = Gross Revenue - Platform Fee
            decimal netEarnings = totalGrossRevenue - totalPlatformFee;

            // Get penalty revenue for this provider
            var penaltyRevenue = await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId
                    && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED || rv.Status == ViolationStatus.RESOLVED))
                .SumAsync(rv => rv.PenaltyAmount);

            // Total Received = Net from Orders + Penalties (same as Provider Dashboard)
            return netEarnings + penaltyRevenue;
        }

        public async Task<IEnumerable<ProviderPaymentDto>> GetAllProviderPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // Build query with optional date filtering
            var query = _context.Orders
                .Where(o => o.Status == OrderStatus.returned);  // Only count returned orders
            
            // Apply date filters if provided
            if (startDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= endDate.Value);
            }
            
            // Get all providers with returned orders (same logic as Revenue Service)
            var providers = await query
                .Include(o => o.Items)  // Include items for commission calculation
                .Include(o => o.Provider)
                    .ThenInclude(p => p.Profile)
                .GroupBy(o => o.ProviderId)
                .Select(g => new
                {
                    ProviderId = g.Key,
                    ProviderName = g.First().Provider.Profile.FullName ?? "Unknown",
                    ProviderEmail = g.First().Provider.Email,
                    Orders = g.ToList()
                })
                .ToListAsync();

            var result = new List<ProviderPaymentDto>();

            foreach (var provider in providers)
            {
                decimal totalGrossRevenue = 0;
                decimal totalPlatformFee = 0;

                // Calculate net revenue (same logic as RevenueRepository.GetTotalEarningsAsync)
                foreach (var order in provider.Orders)
                {
                    // Add gross revenue (Subtotal excludes deposit)
                    totalGrossRevenue += order.Subtotal;

                    // Calculate platform fee for each item
                    foreach (var item in order.Items)
                    {
                        decimal itemRevenue = 0;

                        if (item.TransactionType == TransactionType.rental)
                        {
                            itemRevenue = item.DailyRate * (item.RentalDays ?? 0) * item.Quantity;
                            totalPlatformFee += itemRevenue * 0.20m; // 20% commission
                        }
                        else if (item.TransactionType == TransactionType.purchase)
                        {
                            itemRevenue = item.DailyRate * item.Quantity;
                            totalPlatformFee += itemRevenue * 0.10m; // 10% commission
                        }
                    }
                }

                // Net earnings = Gross Revenue - Platform Fee
                decimal netEarnings = totalGrossRevenue - totalPlatformFee;

                // Get penalty revenue for this provider
                var penaltyRevenue = await _context.RentalViolations
                    .Include(rv => rv.OrderItem)
                        .ThenInclude(oi => oi.Order)
                    .Where(rv => rv.OrderItem.Order.ProviderId == provider.ProviderId
                        && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED || rv.Status == ViolationStatus.RESOLVED))
                    .SumAsync(rv => rv.PenaltyAmount);

                // Total Earned = Net from Orders + Penalties (same as Provider Dashboard)
                decimal totalEarned = netEarnings + penaltyRevenue;

                // Get last payment date
                var lastPayment = provider.Orders
                    .SelectMany(o => o.Transactions)
                    .Where(t => t.Status == TransactionStatus.completed)
                    .Max(t => (DateTime?)t.TransactionDate);

                // Count ALL VALID orders in the date range
                // Valid orders = exclude pending and cancelled (same logic as Provider Dashboard)
                var ordersQuery = _context.Orders
                    .Where(o => o.ProviderId == provider.ProviderId
                             && o.Status != OrderStatus.pending 
                             && o.Status != OrderStatus.cancelled);
                
                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.CreatedAt <= endDate.Value);
                }
                
                var totalOrders = await ordersQuery.CountAsync();

                result.Add(new ProviderPaymentDto
                {
                    ProviderId = provider.ProviderId,
                    ProviderName = provider.ProviderName,
                    ProviderEmail = provider.ProviderEmail,
                    TotalEarned = totalEarned,  // Revenue from returned orders in date range
                    CompletedOrders = totalOrders,  // All valid orders in date range (excl. pending/cancelled)
                    LastPayment = lastPayment
                });
            }

            return result.OrderByDescending(p => p.TotalEarned).ToList();
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
