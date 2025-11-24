using BusinessObject.DTOs.TransactionDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.TransactionRepositories
{
    public class TransactionManagementRepository : ITransactionManagementRepository
    {
        private readonly ShareItDbContext _context;

        public TransactionManagementRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<(List<TransactionManagementDto> Transactions, int TotalCount)> GetAllTransactionsAsync(TransactionFilterDto filter)
        {
            // Tạo danh sách giao dịch từ nhiều nguồn
            var allTransactions = new List<TransactionManagementDto>();

            // 1. Giao dịch từ bảng Transactions (Purchase/Rental)
            var transactionQuery = _context.Transactions
                .Include(t => t.Orders)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                .Include(t => t.Orders)
                    .ThenInclude(o => o.Provider)
                        .ThenInclude(p => p.Profile)
                .AsQueryable();

            var transactions = await transactionQuery.ToListAsync();
            
            foreach (var trans in transactions)
            {
                var order = trans.Orders.FirstOrDefault();
                if (order != null)
                {
                    // Determine transaction type from order items
                    var firstItem = order.Items?.FirstOrDefault();
                    var transactionType = firstItem?.TransactionType ?? TransactionType.rental;
                    
                    allTransactions.Add(new TransactionManagementDto
                    {
                        Id = trans.Id,
                        TransactionDate = trans.TransactionDate,
                        Category = transactionType == TransactionType.purchase 
                            ? TransactionCategory.Purchase 
                            : TransactionCategory.Rental,
                        CategoryDisplay = transactionType == TransactionType.purchase ? "Purchase" : "Rental",
                        Amount = trans.Amount,
                        Status = trans.Status,
                        StatusDisplay = GetStatusDisplay(trans.Status),
                        CustomerId = trans.CustomerId,
                        CustomerName = order.Customer?.Profile?.FullName ?? "N/A",
                        CustomerEmail = order.Customer?.Email ?? "N/A",
                        ProviderId = order.ProviderId,
                        ProviderName = order.Provider?.Profile?.FullName ?? "N/A",
                        ProviderEmail = order.Provider?.Email ?? "N/A",
                        OrderId = order.Id,
                        OrderCode = $"ORD-{order.Id.ToString().Substring(0, 8).ToUpper()}",
                        PaymentMethod = trans.PaymentMethod,
                        Content = trans.Content,
                        DetailInfo = new TransactionDetailInfo
                        {
                            TotalOrderAmount = order.TotalAmount,
                            SecurityDeposit = order.TotalDeposit,
                            OrderType = transactionType,
                            TotalItems = order.Items?.Count ?? 0
                        }
                    });
                }
            }

            // 2. Giao dịch hoàn tiền cọc (DepositRefund)
            var depositRefunds = await _context.DepositRefunds
                .Include(dr => dr.Order)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                .Include(dr => dr.Order)
                    .ThenInclude(o => o.Provider)
                        .ThenInclude(p => p.Profile)
                .Include(dr => dr.RefundBankAccount)
                .Include(dr => dr.ProcessedByAdmin)
                    .ThenInclude(a => a.Profile)
                .ToListAsync();

            foreach (var refund in depositRefunds)
            {
                allTransactions.Add(new TransactionManagementDto
                {
                    Id = refund.Id,
                    TransactionDate = refund.CreatedAt,
                    Category = TransactionCategory.DepositRefund,
                    CategoryDisplay = "Deposit Refund",
                    Amount = refund.RefundAmount,
                    Status = refund.Status,
                    StatusDisplay = GetStatusDisplay(refund.Status),
                    CustomerId = refund.CustomerId,
                    CustomerName = refund.Order?.Customer?.Profile?.FullName ?? "N/A",
                    CustomerEmail = refund.Order?.Customer?.Email ?? "N/A",
                    ProviderId = refund.Order?.ProviderId,
                    ProviderName = refund.Order?.Provider?.Profile?.FullName ?? "N/A",
                    ProviderEmail = refund.Order?.Provider?.Email ?? "N/A",
                    OrderId = refund.OrderId,
                    OrderCode = $"ORD-{refund.OrderId.ToString().Substring(0, 8).ToUpper()}",
                    Notes = refund.Notes,
                    DetailInfo = new TransactionDetailInfo
                    {
                        OriginalDepositAmount = refund.OriginalDepositAmount,
                        TotalPenaltyAmount = refund.TotalPenaltyAmount,
                        RefundAmount = refund.RefundAmount,
                        RefundBankAccountInfo = refund.RefundBankAccount != null 
                            ? $"{refund.RefundBankAccount.BankName} - {refund.RefundBankAccount.AccountNumber}"
                            : "N/A",
                        ExternalTransactionId = refund.ExternalTransactionId,
                        ProcessedAt = refund.ProcessedAt,
                        ProcessedByAdminName = refund.ProcessedByAdmin?.Profile?.FullName ?? "N/A"
                    }
                });
            }

            // 3. Giao dịch rút tiền provider (WithdrawalRequest)
            var withdrawals = await _context.WithdrawalRequests
                .Include(wr => wr.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(wr => wr.BankAccount)
                .Include(wr => wr.ProcessedByAdmin)
                    .ThenInclude(a => a.Profile)
                .ToListAsync();

            foreach (var withdrawal in withdrawals)
            {
                allTransactions.Add(new TransactionManagementDto
                {
                    Id = withdrawal.Id,
                    TransactionDate = withdrawal.RequestDate,
                    Category = TransactionCategory.ProviderWithdrawal,
                    CategoryDisplay = "Provider Withdrawal",
                    Amount = withdrawal.Amount,
                    Status = withdrawal.Status == WithdrawalStatus.Completed 
                        ? TransactionStatus.completed 
                        : withdrawal.Status == WithdrawalStatus.Rejected 
                            ? TransactionStatus.failed 
                            : TransactionStatus.initiated,
                    StatusDisplay = GetWithdrawalStatusDisplay(withdrawal.Status),
                    CustomerId = withdrawal.ProviderId, // Provider acts as customer in this context
                    CustomerName = withdrawal.Provider?.Profile?.FullName ?? "N/A",
                    CustomerEmail = withdrawal.Provider?.Email ?? "N/A",
                    ProviderId = withdrawal.ProviderId,
                    ProviderName = withdrawal.Provider?.Profile?.FullName ?? "N/A",
                    ProviderEmail = withdrawal.Provider?.Email ?? "N/A",
                    Notes = withdrawal.Notes,
                    DetailInfo = new TransactionDetailInfo
                    {
                        WithdrawalAmount = withdrawal.Amount,
                        WithdrawalBankAccountInfo = withdrawal.BankAccount != null
                            ? $"{withdrawal.BankAccount.BankName} - {withdrawal.BankAccount.AccountNumber}"
                            : "N/A",
                        RequestDate = withdrawal.RequestDate,
                        ProcessedAt = withdrawal.ProcessedAt,
                        ProcessedByAdminName = withdrawal.ProcessedByAdmin?.Profile?.FullName ?? "N/A",
                        RejectionReason = withdrawal.RejectionReason,
                        AdminNotes = withdrawal.AdminNotes,
                        ExternalTransactionId = withdrawal.ExternalTransactionId
                    }
                });
            }

            // Apply filters
            var filteredTransactions = allTransactions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var search = filter.SearchQuery.ToLower();
                filteredTransactions = filteredTransactions.Where(t =>
                    t.CustomerName.ToLower().Contains(search) ||
                    t.CustomerEmail.ToLower().Contains(search) ||
                    (t.ProviderName != null && t.ProviderName.ToLower().Contains(search)) ||
                    (t.ProviderEmail != null && t.ProviderEmail.ToLower().Contains(search)) ||
                    (t.OrderCode != null && t.OrderCode.ToLower().Contains(search)) ||
                    t.Id.ToString().ToLower().Contains(search)
                );
            }

            if (filter.StartDate.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.TransactionDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.TransactionDate <= filter.EndDate.Value.AddDays(1));
            }

            if (filter.Category.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.Category == filter.Category.Value);
            }

            if (filter.Status.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.Status == filter.Status.Value);
            }

            if (filter.CustomerId.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.CustomerId == filter.CustomerId.Value);
            }

            if (filter.ProviderId.HasValue)
            {
                filteredTransactions = filteredTransactions.Where(t => t.ProviderId == filter.ProviderId.Value);
            }

            var totalCount = filteredTransactions.Count();

            // Sort by date descending and paginate
            var result = filteredTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return (result, totalCount);
        }

        public async Task<TransactionManagementDto> GetTransactionDetailAsync(Guid transactionId)
        {
            // Try to find in Transactions table first
            var transaction = await _context.Transactions
                .Include(t => t.Orders)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                .Include(t => t.Orders)
                    .ThenInclude(o => o.Provider)
                        .ThenInclude(p => p.Profile)
                .Include(t => t.Orders)
                    .ThenInclude(o => o.Items)
                        .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction != null)
            {
                var order = transaction.Orders.FirstOrDefault();
                if (order != null)
                {
                    // Determine transaction type from order items
                    var firstItem = order.Items?.FirstOrDefault();
                    var transactionType = firstItem?.TransactionType ?? TransactionType.rental;
                    
                    return new TransactionManagementDto
                    {
                        Id = transaction.Id,
                        TransactionDate = transaction.TransactionDate,
                        Category = transactionType == TransactionType.purchase 
                            ? TransactionCategory.Purchase 
                            : TransactionCategory.Rental,
                        CategoryDisplay = transactionType == TransactionType.purchase ? "Purchase" : "Rental",
                        Amount = transaction.Amount,
                        Status = transaction.Status,
                        StatusDisplay = GetStatusDisplay(transaction.Status),
                        CustomerId = transaction.CustomerId,
                        CustomerName = order.Customer?.Profile?.FullName ?? "N/A",
                        CustomerEmail = order.Customer?.Email ?? "N/A",
                        ProviderId = order.ProviderId,
                        ProviderName = order.Provider?.Profile?.FullName ?? "N/A",
                        ProviderEmail = order.Provider?.Email ?? "N/A",
                        OrderId = order.Id,
                        OrderCode = $"ORD-{order.Id.ToString().Substring(0, 8).ToUpper()}",
                        PaymentMethod = transaction.PaymentMethod,
                        Content = transaction.Content,
                        DetailInfo = new TransactionDetailInfo
                        {
                            TotalOrderAmount = order.TotalAmount,
                            SecurityDeposit = order.TotalDeposit,
                            OrderType = transactionType,
                            TotalItems = order.Items?.Count ?? 0
                        }
                    };
                }
            }

            // Try DepositRefund
            var depositRefund = await _context.DepositRefunds
                .Include(dr => dr.Order)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.Profile)
                .Include(dr => dr.Order)
                    .ThenInclude(o => o.Provider)
                        .ThenInclude(p => p.Profile)
                .Include(dr => dr.RefundBankAccount)
                .Include(dr => dr.ProcessedByAdmin)
                    .ThenInclude(a => a.Profile)
                .FirstOrDefaultAsync(dr => dr.Id == transactionId);

            if (depositRefund != null)
            {
                return new TransactionManagementDto
                {
                    Id = depositRefund.Id,
                    TransactionDate = depositRefund.CreatedAt,
                    Category = TransactionCategory.DepositRefund,
                    CategoryDisplay = "Deposit Refund",
                    Amount = depositRefund.RefundAmount,
                    Status = depositRefund.Status,
                    StatusDisplay = GetStatusDisplay(depositRefund.Status),
                    CustomerId = depositRefund.CustomerId,
                    CustomerName = depositRefund.Order?.Customer?.Profile?.FullName ?? "N/A",
                    CustomerEmail = depositRefund.Order?.Customer?.Email ?? "N/A",
                    ProviderId = depositRefund.Order?.ProviderId,
                    ProviderName = depositRefund.Order?.Provider?.Profile?.FullName ?? "N/A",
                    ProviderEmail = depositRefund.Order?.Provider?.Email ?? "N/A",
                    OrderId = depositRefund.OrderId,
                    OrderCode = $"ORD-{depositRefund.OrderId.ToString().Substring(0, 8).ToUpper()}",
                    Notes = depositRefund.Notes,
                    DetailInfo = new TransactionDetailInfo
                    {
                        OriginalDepositAmount = depositRefund.OriginalDepositAmount,
                        TotalPenaltyAmount = depositRefund.TotalPenaltyAmount,
                        RefundAmount = depositRefund.RefundAmount,
                        RefundBankAccountInfo = depositRefund.RefundBankAccount != null 
                            ? $"{depositRefund.RefundBankAccount.BankName} - {depositRefund.RefundBankAccount.AccountNumber}"
                            : "N/A",
                        ExternalTransactionId = depositRefund.ExternalTransactionId,
                        ProcessedAt = depositRefund.ProcessedAt,
                        ProcessedByAdminName = depositRefund.ProcessedByAdmin?.Profile?.FullName ?? "N/A"
                    }
                };
            }

            // Try WithdrawalRequest
            var withdrawal = await _context.WithdrawalRequests
                .Include(wr => wr.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(wr => wr.BankAccount)
                .Include(wr => wr.ProcessedByAdmin)
                    .ThenInclude(a => a.Profile)
                .FirstOrDefaultAsync(wr => wr.Id == transactionId);

            if (withdrawal != null)
            {
                return new TransactionManagementDto
                {
                    Id = withdrawal.Id,
                    TransactionDate = withdrawal.RequestDate,
                    Category = TransactionCategory.ProviderWithdrawal,
                    CategoryDisplay = "Provider Withdrawal",
                    Amount = withdrawal.Amount,
                    Status = withdrawal.Status == WithdrawalStatus.Completed 
                        ? TransactionStatus.completed 
                        : withdrawal.Status == WithdrawalStatus.Rejected 
                            ? TransactionStatus.failed 
                            : TransactionStatus.initiated,
                    StatusDisplay = GetWithdrawalStatusDisplay(withdrawal.Status),
                    CustomerId = withdrawal.ProviderId,
                    CustomerName = withdrawal.Provider?.Profile?.FullName ?? "N/A",
                    CustomerEmail = withdrawal.Provider?.Email ?? "N/A",
                    ProviderId = withdrawal.ProviderId,
                    ProviderName = withdrawal.Provider?.Profile?.FullName ?? "N/A",
                    ProviderEmail = withdrawal.Provider?.Email ?? "N/A",
                    Notes = withdrawal.Notes,
                    DetailInfo = new TransactionDetailInfo
                    {
                        WithdrawalAmount = withdrawal.Amount,
                        WithdrawalBankAccountInfo = withdrawal.BankAccount != null
                            ? $"{withdrawal.BankAccount.BankName} - {withdrawal.BankAccount.AccountNumber}"
                            : "N/A",
                        RequestDate = withdrawal.RequestDate,
                        ProcessedAt = withdrawal.ProcessedAt,
                        ProcessedByAdminName = withdrawal.ProcessedByAdmin?.Profile?.FullName ?? "N/A",
                        RejectionReason = withdrawal.RejectionReason,
                        AdminNotes = withdrawal.AdminNotes,
                        ExternalTransactionId = withdrawal.ExternalTransactionId
                    }
                };
            }

            return null;
        }

        public async Task<TransactionStatisticsDto> GetTransactionStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var stats = new TransactionStatisticsDto();

            // Get all transactions with filters
            var filter = new TransactionFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var (transactions, _) = await GetAllTransactionsAsync(filter);

            // Calculate statistics
            stats.TotalTransactions = transactions.Count;
            stats.CompletedTransactions = transactions.Count(t => t.Status == TransactionStatus.completed);
            stats.PendingTransactions = transactions.Count(t => t.Status == TransactionStatus.initiated);
            stats.FailedTransactions = transactions.Count(t => t.Status == TransactionStatus.failed);

            stats.TotalPurchaseAmount = transactions
                .Where(t => t.Category == TransactionCategory.Purchase && t.Status == TransactionStatus.completed)
                .Sum(t => t.Amount);

            stats.TotalRentalAmount = transactions
                .Where(t => t.Category == TransactionCategory.Rental && t.Status == TransactionStatus.completed)
                .Sum(t => t.Amount);

            stats.TotalDepositRefundAmount = transactions
                .Where(t => t.Category == TransactionCategory.DepositRefund && t.Status == TransactionStatus.completed)
                .Sum(t => t.Amount);

            stats.TotalProviderWithdrawalAmount = transactions
                .Where(t => t.Category == TransactionCategory.ProviderWithdrawal && t.Status == TransactionStatus.completed)
                .Sum(t => t.Amount);

            return stats;
        }

        private string GetStatusDisplay(TransactionStatus status)
        {
            return status switch
            {
                TransactionStatus.completed => "Completed",
                TransactionStatus.initiated => "Pending",
                TransactionStatus.failed => "Failed",
                _ => "Unknown"
            };
        }

        private string GetWithdrawalStatusDisplay(WithdrawalStatus status)
        {
            return status switch
            {
                WithdrawalStatus.Completed => "Completed",
                WithdrawalStatus.Initiated => "Pending",
                WithdrawalStatus.Rejected => "Rejected",
                _ => "Unknown"
            };
        }
    }
}
