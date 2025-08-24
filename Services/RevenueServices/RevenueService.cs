using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.RevenueServices
{
    public class RevenueService : IRevenueService
    {
        private readonly ShareItDbContext _context;
        private readonly IMapper _mapper;

        public RevenueService(ShareItDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<RevenueStatsDto> GetRevenueStatsAsync(Guid userId, string period = "month", DateTime? startDate = null, DateTime? endDate = null)
        {
            var now = DateTime.UtcNow;
            DateTime currentPeriodStart, currentPeriodEnd, previousPeriodStart, previousPeriodEnd;

            // Determine period ranges
            switch (period.ToLower())
            {
                case "week":
                    currentPeriodStart = startDate ?? now.Date.AddDays(-(int)now.DayOfWeek);
                    currentPeriodEnd = endDate ?? currentPeriodStart.AddDays(7);
                    previousPeriodStart = currentPeriodStart.AddDays(-7);
                    previousPeriodEnd = currentPeriodStart;
                    break;
                case "year":
                    currentPeriodStart = startDate ?? new DateTime(now.Year, 1, 1);
                    currentPeriodEnd = endDate ?? new DateTime(now.Year + 1, 1, 1);
                    previousPeriodStart = currentPeriodStart.AddYears(-1);
                    previousPeriodEnd = currentPeriodEnd.AddYears(-1);
                    break;
                default: // month
                    currentPeriodStart = startDate ?? new DateTime(now.Year, now.Month, 1);
                    currentPeriodEnd = endDate ?? currentPeriodStart.AddMonths(1);
                    previousPeriodStart = currentPeriodStart.AddMonths(-1);
                    previousPeriodEnd = currentPeriodStart;
                    break;
            }

            // Get orders for current and previous periods
            var currentOrders = await GetOrdersInPeriod(userId, currentPeriodStart, currentPeriodEnd);
            var previousOrders = await GetOrdersInPeriod(userId, previousPeriodStart, previousPeriodEnd);

            // Calculate stats
            var currentRevenue = currentOrders.Sum(o => o.TotalAmount);
            var previousRevenue = previousOrders.Sum(o => o.TotalAmount);
            var revenueGrowth = previousRevenue > 0 ? ((currentRevenue - previousRevenue) / previousRevenue) * 100 : 0;

            var currentOrderCount = currentOrders.Count;
            var previousOrderCount = previousOrders.Count;
            var orderGrowth = previousOrderCount > 0 ? ((decimal)(currentOrderCount - previousOrderCount) / previousOrderCount) * 100 : 0;

            var currentAvgOrderValue = currentOrderCount > 0 ? currentRevenue / currentOrderCount : 0;
            var previousAvgOrderValue = previousOrderCount > 0 ? previousRevenue / previousOrderCount : 0;
            var avgOrderValueGrowth = previousAvgOrderValue > 0 ? ((currentAvgOrderValue - previousAvgOrderValue) / previousAvgOrderValue) * 100 : 0;

            // Generate chart data
            var chartData = await GenerateChartData(userId, currentPeriodStart, currentPeriodEnd, period);

            // Generate status breakdown
            var statusBreakdown = currentOrders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusBreakdownDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = currentOrderCount > 0 ? (decimal)g.Count() / currentOrderCount * 100 : 0,
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .ToList();

            return new RevenueStatsDto
            {
                CurrentPeriodRevenue = currentRevenue,
                PreviousPeriodRevenue = previousRevenue,
                RevenueGrowthPercentage = revenueGrowth,
                CurrentPeriodOrders = currentOrderCount,
                PreviousPeriodOrders = previousOrderCount,
                OrderGrowthPercentage = orderGrowth,
                AverageOrderValue = currentAvgOrderValue,
                PreviousAverageOrderValue = previousAvgOrderValue,
                AvgOrderValueGrowthPercentage = avgOrderValueGrowth,
                ChartData = chartData,
                StatusBreakdown = statusBreakdown
            };
        }

        public async Task<PayoutSummaryDto> GetPayoutSummaryAsync(Guid userId)
        {
            // Mock implementation - replace with actual logic
            var totalEarnings = await _context.Orders
                .Where(o => o.ProviderId == userId && o.Status == OrderStatus.returned)
                .SumAsync(o => o.TotalAmount);

            var totalPayouts = await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content == "payout")
                .SumAsync(t => t.Amount);

            var currentBalance = totalEarnings - totalPayouts;
            var pendingAmount = await _context.Orders
                .Where(o => o.ProviderId == userId && o.Status == OrderStatus.returned)
                .SumAsync(o => o.TotalAmount);

            var recentPayouts = await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content == "payout")
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .Select(t => new PayoutHistoryDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Date = t.TransactionDate,
                    Status = "completed",
                    BankAccountLast4 = "****",
                    TransactionId = t.Id.ToString()
                })
                .ToListAsync();

            return new PayoutSummaryDto
            {
                CurrentBalance = currentBalance,
                PendingAmount = pendingAmount,
                TotalEarnings = totalEarnings,
                TotalPayouts = totalPayouts,
                NextPayoutDate = DateTime.UtcNow.AddDays(7),
                RecentPayouts = recentPayouts
            };
        }

        public async Task<List<PayoutHistoryDto>> GetPayoutHistoryAsync(Guid userId, int page = 1, int pageSize = 10)
        {
            return await _context.Transactions
                .Where(t => t.CustomerId == userId && t.Content.StartsWith("Thanh toán")) //t.Content == "payout"
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new PayoutHistoryDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Date = t.TransactionDate,
                    Status = "completed",
                    BankAccountLast4 = "****",
                    TransactionId = t.Id.ToString()
                })
                .ToListAsync();
        }

        public async Task<List<BankAccountDto>> GetBankAccountsAsync(Guid userId)
        {
            var bankAccounts = await _context.BankAccounts
                .Include(ba => ba.Provider)
                .ThenInclude(p => p.Profile)
                .Where(ba => ba.ProviderId == userId)
                .ToListAsync();

            return bankAccounts.Select(ba => new BankAccountDto
            {
                Id = ba.Id,
                BankName = ba.BankName,
                AccountNumber = ba.AccountNumber,
                AccountHolderName = ba.Provider?.Profile?.FullName ?? "Unknown",
                IsPrimary = ba.IsPrimary,
                CreatedAt = DateTime.UtcNow // BankAccount doesn't have CreatedAt, using current time
            }).ToList();
        }

        public async Task<BankAccountDto> CreateBankAccountAsync(Guid userId, CreateBankAccountDto dto)
        {
            var bankAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                ProviderId = userId,
                BankName = dto.BankName,
                AccountNumber = dto.AccountNumber,
                IsPrimary = dto.SetAsPrimary
            };

            if (dto.SetAsPrimary)
            {
                // Remove primary status from other accounts
                await _context.BankAccounts
                    .Where(ba => ba.ProviderId == userId && ba.IsPrimary)
                    .ExecuteUpdateAsync(ba => ba.SetProperty(b => b.IsPrimary, false));
            }

            _context.BankAccounts.Add(bankAccount);
            await _context.SaveChangesAsync();

            return _mapper.Map<BankAccountDto>(bankAccount);
        }

        public async Task<bool> UpdateBankAccountAsync(Guid userId, Guid accountId, CreateBankAccountDto dto)
        {
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == accountId && ba.ProviderId == userId);

            if (bankAccount == null) return false;

            bankAccount.BankName = dto.BankName;
            bankAccount.AccountNumber = dto.AccountNumber;

            if (dto.SetAsPrimary && !bankAccount.IsPrimary)
            {
                await _context.BankAccounts
                    .Where(ba => ba.ProviderId == userId && ba.IsPrimary)
                    .ExecuteUpdateAsync(ba => ba.SetProperty(b => b.IsPrimary, false));

                bankAccount.IsPrimary = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteBankAccountAsync(Guid userId, Guid accountId)
        {
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == accountId && ba.ProviderId == userId);

            if (bankAccount == null) return false;

            _context.BankAccounts.Remove(bankAccount);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetPrimaryBankAccountAsync(Guid userId, Guid accountId)
        {
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == accountId && ba.ProviderId == userId);

            if (bankAccount == null) return false;

            // Remove primary status from all accounts
            await _context.BankAccounts
                .Where(ba => ba.ProviderId == userId)
                .ExecuteUpdateAsync(ba => ba.SetProperty(b => b.IsPrimary, false));

            // Set new primary
            bankAccount.IsPrimary = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RequestPayoutAsync(Guid userId, decimal amount)
        {
            // Mock implementation - replace with actual payout logic
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                Amount = amount,
                Status = TransactionStatus.completed,
                Content = "payout",
                TransactionDate = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<List<Order>> GetOrdersInPeriod(Guid userId, DateTime start, DateTime end)
        {
            return await _context.Orders
                .Where(o => o.ProviderId == userId && o.CreatedAt >= start && o.CreatedAt < end)
                .ToListAsync();
        }

        private async Task<List<RevenueChartDataDto>> GenerateChartData(Guid userId, DateTime start, DateTime end, string period)
        {
            var orders = await GetOrdersInPeriod(userId, start, end);

            var chartData = new List<RevenueChartDataDto>();
            var current = start;

            while (current < end)
            {
                DateTime periodEnd;
                string periodLabel;

                switch (period.ToLower())
                {
                    case "week":
                        periodEnd = current.AddDays(1);
                        periodLabel = current.ToString("MMM dd");
                        break;
                    case "year":
                        periodEnd = current.AddMonths(1);
                        periodLabel = current.ToString("MMM yyyy");
                        break;
                    default: // month
                        periodEnd = current.AddDays(1);
                        periodLabel = current.ToString("MMM dd");
                        break;
                }

                var periodOrders = orders.Where(o => o.CreatedAt >= current && o.CreatedAt < periodEnd);

                chartData.Add(new RevenueChartDataDto
                {
                    Period = periodLabel,
                    Revenue = periodOrders.Sum(o => o.TotalAmount),
                    OrderCount = periodOrders.Count(),
                    Date = current
                });

                current = periodEnd;
            }

            return chartData;
        }
    }
}
