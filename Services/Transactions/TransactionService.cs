using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Transactions;
using BusinessObject.Models;
using BusinessObject.DTOs.VNPay.Request;
using BusinessObject.DTOs.VNPay;
using System;
using System.Text.RegularExpressions;
using BusinessObject.DTOs.BankQR;
using Microsoft.Extensions.Options;
using Services.NotificationServices;
using Services.OrderServices;

namespace LibraryManagement.Services.Payments.Transactions
{
    public class TransactionService : ITransactionService
    {
        private readonly ShareItDbContext _dbContext;
        private readonly ILogger<TransactionService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IOrderService _orderService;
        private readonly BankQrConfig _bankQrConfig;

        public TransactionService(ShareItDbContext dbContext, ILogger<TransactionService> logger, IOptions<BankQrConfig> bankQrOptions, INotificationService notificationService, IOrderService orderService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _notificationService = notificationService;
            _orderService = orderService;
            _bankQrConfig = bankQrOptions.Value;
        }

        // Lấy danh sách giao dịch của 1 user (Customer)
        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid customerId)
        {
            return await _dbContext.Transactions
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.TransactionDate)
                .AsNoTracking()
                .ToListAsync();
        }

        // Lưu giao dịch mới
        public async Task<Transaction> SaveTransactionAsync(Transaction transaction)
        {
            try
            {
                _dbContext.Transactions.Add(transaction);
                await _dbContext.SaveChangesAsync();
                return transaction;
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Error saving transaction to database. Transaction details: {@Transaction}", transaction);
                throw;
            }
        }

        // Xử lý webhook từ SEPay (dựa vào OrderId và Amount)
        public async Task<bool> ProcessSepayWebhookAsync(SepayWebhookRequest request)
        {
            string expectedBankAccount = _bankQrConfig.AccountNumber;

            if (request.BankAccount != expectedBankAccount)
            {
                _logger.LogWarning("Invalid bank account: {BankAccount}", request.BankAccount);
                return false;
            }

            var orderId = ExtractOrderIdFromContent(request.Content);
            if (orderId == null)
            {
                _logger.LogWarning("OrderId not found in SEPay content: {Content}", request.Content);
                return false;
            }

            var transaction = await _dbContext.Transactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Status != BusinessObject.Enums.TransactionStatus.completed);

            if (transaction == null)
            {
                _logger.LogWarning("Transaction not found for OrderId: {OrderId}", orderId);
                return false;
            }

            if (transaction.Amount != request.Amount)
            {
                _logger.LogWarning("Amount mismatch for OrderId {OrderId}: expected {Expected}, received {Received}",
                    orderId, transaction.Amount, request.Amount);
                return false;
            }

            if (request.IsSuccess)
            {
                transaction.Status = BusinessObject.Enums.TransactionStatus.completed;
                transaction.PaymentMethod = "Bank Transfer - SEPay";
                transaction.TransactionDate = DateTime.UtcNow;

                _logger.LogInformation("Transaction for Order {OrderId} marked as Completed", orderId);
                // TODO: Cập nhật trạng thái đơn hàng nếu cần
                await _orderService.CompleteTransactionAsync(orderId.Value);
            }
            else
            {
                transaction.Status = BusinessObject.Enums.TransactionStatus.failed;
                transaction.PaymentMethod = "Bank Transfer - SEPay";
                transaction.TransactionDate = DateTime.UtcNow;

                await _orderService.FailTransactionAsync(orderId.Value);
                _logger.LogWarning("Transaction for Order {OrderId} marked as Failed", orderId);
            }
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<Transaction?> GetTransactionByIdAsync(Guid transactionId)
        {
            return await _dbContext.Transactions.FindAsync(transactionId);
        }

        private Guid? ExtractOrderIdFromContent(string content)
        {
            var match = Regex.Match(content, @"PAYORDER([A-F0-9]{32})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string guidNoDash = match.Groups[1].Value;
                if (guidNoDash.Length == 32)
                {
                    string formattedGuid = $"{guidNoDash.Substring(0, 8)}-{guidNoDash.Substring(8, 4)}-{guidNoDash.Substring(12, 4)}-{guidNoDash.Substring(16, 4)}-{guidNoDash.Substring(20, 12)}";
                    if (Guid.TryParse(formattedGuid, out var orderId))
                    {
                        return orderId;
                    }
                }
            }
            return null;
        }
    }
}