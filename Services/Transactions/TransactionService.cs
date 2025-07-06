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
using Common.Utilities.VNPAY.Common.Utilities.VNPAY;
using BusinessObject.Enums;

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
            // --- 1. Kiểm tra tài khoản ngân hàng hợp lệ ---
            if (request.BankAccount != _bankQrConfig.AccountNumber)
            {
                _logger.LogWarning("Invalid bank account received in webhook: {BankAccount}", request.BankAccount);
                return false;
            }

            // --- 2. Parse danh sách OrderId từ request.Description ---
            List<Guid> orderIds;
            try
            {
                if (request.Content.StartsWith("OIDS "))
                {
                    // Bỏ đi tiền tố "OIDS "
                    string contentWithoutPrefix = request.Content.Substring(5);

                    // Tách chuỗi thành các "từ" dựa trên dấu cách
                    string[] words = contentWithoutPrefix.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    List<Guid> foundGuids = new List<Guid>();
                    foreach (string word in words)
                    {
                        // Kiểm tra xem "từ" có phải là một GUID hợp lệ không
                        if (Guid.TryParse(word, out Guid parsedGuid))
                        {
                            foundGuids.Add(parsedGuid);
                        }
                    }
                    orderIds = foundGuids;
                }
                else
                {
                    _logger.LogWarning("Invalid description format: {Description}", request.Description);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse OrderIds from Description: {Description}", request.Description);
                return false;
            }

            if (!orderIds.Any())
            {
                _logger.LogWarning("No valid order IDs extracted from description: {Description}", request.Description);
                return false;
            }

            // --- 3. Bắt đầu transaction DB ---
            using (var dbTransaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    if (request.IsSuccess)
                    {
                        foreach (var orderId in orderIds)
                        {
                            var order = await _orderService.GetOrderDetailAsync(orderId);
                            if (order == null)
                            {
                                _logger.LogWarning("Order not found: {OrderId}", orderId);
                                continue;
                            }

                            // Tạo giao dịch cho từng đơn hàng
                            var transaction = new Transaction
                            {
                                Id = Guid.NewGuid(),
                                OrderId = order.Id,
                                CustomerId = order.CustomerId,
                                ProviderId = order.ProviderId,
                                Amount = order.TotalAmount,
                                Status = BusinessObject.Enums.TransactionStatus.completed,
                                PaymentMethod = "Bank Transfer - SEPay",
                                TransactionDate = DateTime.UtcNow,
                                Content = $"Payment for order {order.Id}"
                            };

                            await _dbContext.Transactions.AddAsync(transaction);
                            await _orderService.ChangeOrderStatus(orderId, OrderStatus.approved);
                            await _orderService.CompleteTransactionAsync(orderId);
                        }

                        _logger.LogInformation("Webhook payment success. Orders: {OrderIds}", string.Join(", ", orderIds));
                    }
                    else
                    {
                        foreach (var orderId in orderIds)
                        {
                            await _orderService.FailTransactionAsync(orderId);
                        }

                        _logger.LogWarning("Webhook payment failed. Orders: {OrderIds}", string.Join(", ", orderIds));
                    }

                    await _dbContext.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SEPay webhook. Rolling back.");
                    await dbTransaction.RollbackAsync();
                    return false;
                }
            }
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