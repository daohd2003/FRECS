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
using BusinessObject.DTOs.TransactionsDto;
using AutoMapper.QueryableExtensions;
using AutoMapper;
using BusinessObject.DTOs.OrdersDto;
using Services.DiscountCodeServices;

namespace LibraryManagement.Services.Payments.Transactions
{
    public class TransactionService : ITransactionService
    {
        private readonly ShareItDbContext _dbContext;
        private readonly ILogger<TransactionService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IOrderService _orderService;
        private readonly BankQrConfig _bankQrConfig;
        private readonly IMapper _mapper;
        private readonly IDiscountCodeService _discountCodeService;

        public TransactionService(ShareItDbContext dbContext, ILogger<TransactionService> logger, IOptions<BankQrConfig> bankQrOptions, INotificationService notificationService, IOrderService orderService, IMapper mapper, IDiscountCodeService discountCodeService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _notificationService = notificationService;
            _orderService = orderService;
            _bankQrConfig = bankQrOptions.Value;
            _mapper = mapper;
            _discountCodeService = discountCodeService;
        }

        // Lấy danh sách giao dịch của 1 user (Customer)
        public async Task<IEnumerable<TransactionSummaryDto>> GetUserTransactionsAsync(Guid customerId)
        {
            return await _dbContext.Transactions
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.TransactionDate)
                // Dùng ProjectTo để map hiệu quả và tránh tải dữ liệu thừa
                .Select(t => new TransactionSummaryDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Status = t.Status,
                    PaymentMethod = t.PaymentMethod,
                    TransactionDate = t.TransactionDate,
                    Orders = t.Orders.Select(o => new OrderProviderPairDto
                    {
                        OrderId = o.Id,
                        ProviderId = o.ProviderId,
                        OrderAmount = o.TotalAmount
                    }).ToList()
                })
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
            Guid transactionId;
            try
            {
                if (request.Content != null && request.Content.StartsWith("TID "))
                {
                    string contentWithoutPrefix = request.Content.Substring(4).Trim();
                    string[] parts = contentWithoutPrefix.Split(' ');
                    string idString = parts.FirstOrDefault();
                    if (!Guid.TryParse(idString, out transactionId))
                    {
                        _logger.LogWarning("Invalid GUID format in webhook content: {Content}", request.Content);
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid content format. Expected 'TID <GUID>'. Received: {Content}", request.Content);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse OrderIds from Description: {Description}", request.Description);
                return false;
            }

            // --- 3. Bắt đầu transaction DB ---
            using (var dbTransaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Tìm Transaction và các Order liên quan
                    var transaction = await _dbContext.Transactions
                                                      .Include(t => t.Orders) // Quan trọng: Nạp các Order liên quan
                                                      .FirstOrDefaultAsync(t => t.Id == transactionId);

                    if (transaction == null)
                    {
                        _logger.LogWarning("Transaction not found: {TransactionId}", transactionId);
                        await dbTransaction.RollbackAsync();
                        return false;
                    }

                    if (transaction.Status != BusinessObject.Enums.TransactionStatus.initiated)
                    {
                        _logger.LogWarning("Transaction {TransactionId} has already been processed.", transactionId);
                        await dbTransaction.CommitAsync(); // Giao dịch đã được xử lý, coi như thành công
                        return true;
                    }

                    // Lấy danh sách các Order ID để log
                    var orderIds = transaction.Orders.Select(o => o.Id).ToList();

                    if (request.IsSuccess)
                    {
                        // Cập nhật trạng thái transaction tổng
                        transaction.Status = BusinessObject.Enums.TransactionStatus.completed;
                        transaction.Content = $"Paid successfully via webhook. Ref: {request.ReferenceCode}";

                        // Cập nhật trạng thái cho từng order
                        foreach (var order in transaction.Orders)
                        {
                            _logger.LogInformation("[PAYMENT DEBUG] Processing Order {OrderId}, DiscountCodeId: {DiscountCodeId}, DiscountAmount: {DiscountAmount}", 
                                order.Id, order.DiscountCodeId, order.DiscountAmount);
                            
                            await _orderService.ChangeOrderStatus(order.Id, OrderStatus.approved);
                            await _orderService.ClearCartItemsForOrderAsync(order);
                            
                            // Record discount code usage if order has discount code
                            if (order.DiscountCodeId.HasValue)
                            {
                                try
                                {
                                    _logger.LogInformation("[PAYMENT DEBUG] Recording discount code usage - UserId: {UserId}, DiscountCodeId: {DiscountCodeId}, OrderId: {OrderId}", 
                                        order.CustomerId, order.DiscountCodeId.Value, order.Id);
                                    
                                    await _discountCodeService.RecordDiscountCodeUsageAsync(
                                        order.CustomerId, 
                                        order.DiscountCodeId.Value, 
                                        order.Id);
                                    
                                    _logger.LogInformation("✅ Recorded discount code usage for Order {OrderId}, DiscountCode {DiscountCodeId}", 
                                        order.Id, order.DiscountCodeId.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "❌ Failed to record discount code usage for Order {OrderId}", order.Id);
                                    // Don't fail the whole transaction if discount recording fails
                                }
                            }
                            else
                            {
                                _logger.LogInformation("[PAYMENT DEBUG] Order {OrderId} has no discount code", order.Id);
                            }
                        }
                        _logger.LogInformation("Webhook payment success for Transaction {TransactionId}. Orders: {OrderIds}", transaction.Id, string.Join(", ", orderIds));
                    }
                    else
                    {
                        transaction.Status = BusinessObject.Enums.TransactionStatus.failed;
                        _logger.LogWarning("Webhook payment failed for Transaction {TransactionId}. Orders: {OrderIds}", transaction.Id, string.Join(", ", orderIds));

                        foreach (var orderId in orderIds)
                        {
                            await _orderService.FailTransactionAsync(orderId);
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SEPay webhook for TID {TransactionId}. Rolling back.", transactionId);
                    await dbTransaction.RollbackAsync();
                    return false;
                }
            }
        }

        public async Task<TransactionSummaryDto?> GetTransactionByIdAsync(Guid transactionId)
        {
            var dto = await _dbContext.Transactions
                .Where(t => t.Id == transactionId)
                .ProjectTo<TransactionSummaryDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            return dto;
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