using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RevenueRepositories;
using Repositories.TransactionRepositories;
using Repositories.BankAccountRepositories;
using Repositories.WithdrawalRepositories;
using Repositories.SystemConfigRepositories;

namespace Services.RevenueServices
{
    public class RevenueService : IRevenueService
    {
        private readonly IRevenueRepository _revenueRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBankAccountRepository _bankAccountRepository;
        private readonly IWithdrawalRepository _withdrawalRepository;
        private readonly ISystemConfigRepository _systemConfigRepository;
        private readonly IMapper _mapper;

        public RevenueService(
            IRevenueRepository revenueRepository,
            ITransactionRepository transactionRepository,
            IBankAccountRepository bankAccountRepository,
            IWithdrawalRepository withdrawalRepository,
            ISystemConfigRepository systemConfigRepository,
            IMapper mapper)
        {
            _revenueRepository = revenueRepository;
            _transactionRepository = transactionRepository;
            _bankAccountRepository = bankAccountRepository;
            _withdrawalRepository = withdrawalRepository;
            _systemConfigRepository = systemConfigRepository;
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

            // Get orders for current and previous periods (only returned for revenue calculation)
            var currentReturnedOrders = await GetOrdersInPeriod(userId, currentPeriodStart, currentPeriodEnd);
            var previousReturnedOrders = await GetOrdersInPeriod(userId, previousPeriodStart, previousPeriodEnd);

            // Get ALL orders for status breakdown
            var currentAllOrders = await _revenueRepository.GetAllOrdersInPeriodAsync(userId, currentPeriodStart, currentPeriodEnd);
            var previousAllOrders = await _revenueRepository.GetAllOrdersInPeriodAsync(userId, previousPeriodStart, previousPeriodEnd);

            // Filter valid orders (exclude pending and cancelled - not considered real orders)
            var currentValidOrders = currentAllOrders
                .Where(o => o.Status != OrderStatus.pending && o.Status != OrderStatus.cancelled)
                .ToList();
            var previousValidOrders = previousAllOrders
                .Where(o => o.Status != OrderStatus.pending && o.Status != OrderStatus.cancelled)
                .ToList();

            // Calculate revenue (using ONLY returned orders) - Use Subtotal (excludes deposit)
            var currentRevenue = currentReturnedOrders.Sum(o => o.Subtotal);
            var previousRevenue = previousReturnedOrders.Sum(o => o.Subtotal);
            var revenueGrowth = previousRevenue > 0 ? ((currentRevenue - previousRevenue) / previousRevenue) * 100 : 0;

            // Calculate order count (using valid orders - exclude pending and cancelled)
            var currentOrderCount = currentValidOrders.Count;
            var previousOrderCount = previousValidOrders.Count;
            var orderGrowth = previousOrderCount > 0 ? ((decimal)(currentOrderCount - previousOrderCount) / previousOrderCount) * 100 : 0;

            // Calculate average order value (using returned orders for accuracy)
            var currentReturnedCount = currentReturnedOrders.Count;
            var previousReturnedCount = previousReturnedOrders.Count;
            var currentAvgOrderValue = currentReturnedCount > 0 ? currentRevenue / currentReturnedCount : 0;
            var previousAvgOrderValue = previousReturnedCount > 0 ? previousRevenue / previousReturnedCount : 0;
            var avgOrderValueGrowth = previousAvgOrderValue > 0 ? ((currentAvgOrderValue - previousAvgOrderValue) / previousAvgOrderValue) * 100 : 0;

            // Calculate platform commission breakdown using rates from database
            var currentCommissionBreakdown = await CalculateCommissionBreakdownAsync(currentReturnedOrders);
            var previousCommissionBreakdown = await CalculateCommissionBreakdownAsync(previousReturnedOrders);
            
            var currentPlatformFee = currentCommissionBreakdown.TotalFee;
            var previousPlatformFee = previousCommissionBreakdown.TotalFee;
            var platformFeeGrowth = previousPlatformFee > 0 ? ((currentPlatformFee - previousPlatformFee) / previousPlatformFee) * 100 : 0;
            
            // Get penalty revenue (from RentalViolations with status CUSTOMER_ACCEPTED or RESOLVED)
            var currentPenaltyRevenue = await _revenueRepository.GetPenaltyRevenueInPeriodAsync(userId, currentPeriodStart, currentPeriodEnd);
            var previousPenaltyRevenue = await _revenueRepository.GetPenaltyRevenueInPeriodAsync(userId, previousPeriodStart, previousPeriodEnd);
            
            // Calculate net revenue (after platform fees + penalty revenue)
            var currentNetRevenueFromOrders = currentRevenue - currentPlatformFee;
            var previousNetRevenueFromOrders = previousRevenue - previousPlatformFee;
            
            var currentNetRevenue = currentNetRevenueFromOrders + currentPenaltyRevenue;
            var previousNetRevenue = previousNetRevenueFromOrders + previousPenaltyRevenue;
            var netRevenueGrowth = previousNetRevenue > 0 ? ((currentNetRevenue - previousNetRevenue) / previousNetRevenue) * 100 : 0;

            // Generate chart data (using only returned orders)
            var chartData = await GenerateChartData(userId, currentPeriodStart, currentPeriodEnd, period);

            // Generate status breakdown (using ALL orders to show all statuses)
            var totalAllOrders = currentAllOrders.Count;
            var statusBreakdown = currentAllOrders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusBreakdownDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = totalAllOrders > 0 ? (decimal)g.Count() / totalAllOrders * 100 : 0,
                    Revenue = g.Key == OrderStatus.returned ? g.Sum(o => o.Subtotal) : 0 // Only count revenue for returned status (excludes deposit)
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
                
                // Net Revenue & Platform Fee
                NetRevenue = currentNetRevenue,
                PreviousNetRevenue = previousNetRevenue,
                NetRevenueGrowthPercentage = netRevenueGrowth,
                NetRevenueFromOrders = currentNetRevenueFromOrders,
                NetRevenueFromPenalties = currentPenaltyRevenue,
                PreviousNetRevenueFromOrders = previousNetRevenueFromOrders,
                PreviousNetRevenueFromPenalties = previousPenaltyRevenue,
                PlatformFee = currentPlatformFee,
                PreviousPlatformFee = previousPlatformFee,
                PlatformFeeGrowthPercentage = platformFeeGrowth,
                
                // Breakdown by transaction type
                RentalRevenue = currentCommissionBreakdown.RentalRevenue,
                PurchaseRevenue = currentCommissionBreakdown.PurchaseRevenue,
                RentalFee = currentCommissionBreakdown.RentalFee,
                PurchaseFee = currentCommissionBreakdown.PurchaseFee,
                
                ChartData = chartData,
                StatusBreakdown = statusBreakdown
            };
        }

        public async Task<PayoutSummaryDto> GetPayoutSummaryAsync(Guid userId)
        {
            var totalEarnings = await _revenueRepository.GetTotalEarningsAsync(userId);
            // Lấy tổng số tiền đã rút thành công từ bảng WithdrawalRequests
            var totalPayouts = await _withdrawalRepository.GetTotalCompletedAmountAsync(userId);
            var currentBalance = totalEarnings - totalPayouts;
            var pendingAmount = await _withdrawalRepository.GetTotalPendingAmountAsync(userId);

            var recentTransactions = await _transactionRepository.GetRecentPayoutsAsync(userId, 5);
            var recentPayouts = recentTransactions.Select(t => new PayoutHistoryDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Date = t.TransactionDate,
                Status = "completed",
                BankAccountLast4 = "****",
                TransactionId = t.Id.ToString()
            }).ToList();

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
            var transactions = await _transactionRepository.GetPayoutHistoryAsync(userId, page, pageSize);
            
            return transactions.Select(t => new PayoutHistoryDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Date = t.TransactionDate,
                Status = "completed",
                BankAccountLast4 = "****",
                TransactionId = t.Id.ToString()
            }).ToList();
        }

        public async Task<List<BankAccountDto>> GetBankAccountsAsync(Guid userId)
        {
            var bankAccounts = await _bankAccountRepository.GetBankAccountsByUserAsync(userId);

            return bankAccounts.Select(ba => new BankAccountDto
            {
                Id = ba.Id,
                BankName = ba.BankName,
                AccountNumber = ba.AccountNumber,
                AccountHolderName = ba.AccountHolderName,
                RoutingNumber = ba.RoutingNumber,
                IsPrimary = ba.IsPrimary,
                CreatedAt = DateTime.UtcNow // BankAccount doesn't have CreatedAt, using current time
            }).ToList();
        }

        public async Task<BankAccountDto> CreateBankAccountAsync(Guid userId, CreateBankAccountDto dto)
        {
            var bankAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BankName = dto.BankName,
                AccountNumber = dto.AccountNumber,
                AccountHolderName = dto.AccountHolderName,
                RoutingNumber = dto.RoutingNumber,
                IsPrimary = dto.SetAsPrimary
            };

            if (dto.SetAsPrimary)
            {
                await _bankAccountRepository.RemovePrimaryStatusAsync(userId);
            }

            await _bankAccountRepository.AddAsync(bankAccount);

            return _mapper.Map<BankAccountDto>(bankAccount);
        }

        public async Task<bool> UpdateBankAccountAsync(Guid userId, Guid accountId, CreateBankAccountDto dto)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, userId);

            if (bankAccount == null) return false;

            bankAccount.BankName = dto.BankName;
            bankAccount.AccountNumber = dto.AccountNumber;
            bankAccount.AccountHolderName = dto.AccountHolderName;
            bankAccount.RoutingNumber = dto.RoutingNumber;

            if (dto.SetAsPrimary && !bankAccount.IsPrimary)
            {
                await _bankAccountRepository.RemovePrimaryStatusAsync(userId);
                bankAccount.IsPrimary = true;
            }

            await _bankAccountRepository.UpdateAsync(bankAccount);
            return true;
        }

        public async Task<bool> DeleteBankAccountAsync(Guid userId, Guid accountId)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, userId);

            if (bankAccount == null) return false;

            await _bankAccountRepository.DeleteAsync(accountId);
            return true;
        }

        public async Task<bool> SetPrimaryBankAccountAsync(Guid userId, Guid accountId)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAndUserAsync(accountId, userId);

            if (bankAccount == null) return false;

            await _bankAccountRepository.RemovePrimaryStatusAsync(userId);

            bankAccount.IsPrimary = true;
            await _bankAccountRepository.UpdateAsync(bankAccount);
            return true;
        }

        public async Task<bool> RequestPayoutAsync(Guid userId, decimal amount)
        {
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                Amount = amount,
                Status = TransactionStatus.completed,
                Content = "payout",
                TransactionDate = DateTime.UtcNow
            };

            await _transactionRepository.AddTransactionAsync(transaction);
            return true;
        }

        private async Task<List<Order>> GetOrdersInPeriod(Guid userId, DateTime start, DateTime end)
        {
            return await _revenueRepository.GetOrdersInPeriodAsync(userId, start, end);
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
                    Revenue = periodOrders.Sum(o => o.Subtotal),  // Use Subtotal (excludes deposit)
                    OrderCount = periodOrders.Count(),
                    Date = current
                });

                current = periodEnd;
            }

            return chartData;
        }

        /// <summary>
        /// Calculate platform commission breakdown from OrderItems
        /// Uses commission rates from database configuration
        /// </summary>
        private async Task<CommissionBreakdown> CalculateCommissionBreakdownAsync(List<Order> orders)
        {
            decimal rentalRevenue = 0;
            decimal purchaseRevenue = 0;

            foreach (var order in orders)
            {
                foreach (var item in order.Items)
                {
                    if (item.TransactionType == TransactionType.rental)
                    {
                        // Rental: DailyRate × RentalDays × Quantity
                        var itemRevenue = item.DailyRate * (item.RentalDays ?? 0) * item.Quantity;
                        rentalRevenue += itemRevenue;
                    }
                    else if (item.TransactionType == TransactionType.purchase)
                    {
                        // Purchase: DailyRate × Quantity (DailyRate is used as unit price for purchase)
                        var itemRevenue = item.DailyRate * item.Quantity;
                        purchaseRevenue += itemRevenue;
                    }
                }
            }

            // Get commission rates from database
            var rentalCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("RENTAL_COMMISSION_RATE");
            var purchaseCommissionRate = await _systemConfigRepository.GetCommissionRateAsync("PURCHASE_COMMISSION_RATE");

            var rentalFee = rentalRevenue * rentalCommissionRate;
            var purchaseFee = purchaseRevenue * purchaseCommissionRate;

            return new CommissionBreakdown
            {
                RentalRevenue = rentalRevenue,
                PurchaseRevenue = purchaseRevenue,
                RentalFee = rentalFee,
                PurchaseFee = purchaseFee,
                TotalFee = rentalFee + purchaseFee
            };
        }
    }

    /// <summary>
    /// Helper class for commission calculation
    /// </summary>
    internal class CommissionBreakdown
    {
        public decimal RentalRevenue { get; set; }
        public decimal PurchaseRevenue { get; set; }
        public decimal RentalFee { get; set; }
        public decimal PurchaseFee { get; set; }
        public decimal TotalFee { get; set; }
    }
}
