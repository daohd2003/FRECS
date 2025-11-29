using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.DepositRefundRepositories
{
    public class DepositRefundRepository : IDepositRefundRepository
    {
        private readonly ShareItDbContext _context;

        public DepositRefundRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DepositRefundDto>> GetAllRefundRequestsAsync(TransactionStatus? status = null)
        {
            var query = _context.DepositRefunds
                .AsNoTracking()
                .Include(dr => dr.Customer)
                    .ThenInclude(c => c.Profile)
                .Include(dr => dr.Order)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(dr => dr.Status == status.Value);
            }

            var refunds = await query
                .OrderByDescending(dr => dr.CreatedAt)
                .Select(dr => new DepositRefundDto
                {
                    Id = dr.Id,
                    RefundCode = $"RF-{dr.Id.ToString().Substring(0, 8).ToUpper()}",
                    CreatedAt = dr.CreatedAt,
                    CustomerId = dr.CustomerId,
                    CustomerName = dr.Customer.Profile != null ? dr.Customer.Profile.FullName : "N/A",
                    CustomerEmail = dr.Customer.Email,
                    OrderId = dr.OrderId,
                    OrderCode = $"ORD-{dr.OrderId.ToString().Substring(0, 8).ToUpper()}",
                    OriginalDepositAmount = dr.OriginalDepositAmount,
                    TotalPenaltyAmount = dr.TotalPenaltyAmount,
                    RefundAmount = dr.RefundAmount,
                    RefundBankAccountId = dr.RefundBankAccountId,
                    Status = dr.Status,
                    Notes = dr.Notes,
                    ExternalTransactionId = dr.ExternalTransactionId
                })
                .ToListAsync();

            // Get bank info for each customer (prefer primary account)
            foreach (var refund in refunds)
            {
                var bankAccount = await _context.BankAccounts
                    .Where(ba => ba.UserId == refund.CustomerId)
                    .OrderByDescending(ba => ba.IsPrimary) // Primary account first
                    .ThenBy(ba => ba.Id) // Then by creation order
                    .FirstOrDefaultAsync();

                if (bankAccount != null)
                {
                    // Set customer's bank info for display
                    refund.CustomerBankName = bankAccount.BankName;
                    refund.CustomerAccountNumber = bankAccount.AccountNumber;
                    refund.CustomerAccountHolderName = bankAccount.AccountHolderName;
                }
            }

            return refunds;
        }

        public async Task<DepositRefundDetailDto?> GetRefundDetailAsync(Guid refundId)
        {
            var refund = await _context.DepositRefunds
                .AsNoTracking()
                .Include(dr => dr.Customer)
                    .ThenInclude(c => c.Profile)
                .Include(dr => dr.Order)
                .Include(dr => dr.ProcessedByAdmin)
                    .ThenInclude(a => a!.Profile)
                .Where(dr => dr.Id == refundId)
                .FirstOrDefaultAsync();

            if (refund == null) return null;

            var detail = new DepositRefundDetailDto
            {
                Id = refund.Id,
                RefundCode = $"RF-{refund.Id.ToString().Substring(0, 8).ToUpper()}",
                CreatedAt = refund.CreatedAt,
                CustomerId = refund.CustomerId,
                CustomerName = refund.Customer.Profile?.FullName ?? "N/A",
                CustomerEmail = refund.Customer.Email,
                CustomerPhone = refund.Customer.Profile?.Phone ?? "N/A",
                OrderId = refund.OrderId,
                OrderCode = $"ORD-{refund.OrderId.ToString().Substring(0, 8).ToUpper()}",
                OrderReturnedDate = refund.Order.UpdatedAt,
                OriginalDepositAmount = refund.OriginalDepositAmount,
                TotalPenaltyAmount = refund.TotalPenaltyAmount,
                RefundAmount = refund.RefundAmount,
                RefundBankAccountId = refund.RefundBankAccountId,
                Status = refund.Status,
                ProcessedByAdminId = refund.ProcessedByAdminId,
                ProcessedByAdminName = refund.ProcessedByAdmin?.Profile?.FullName,
                ProcessedAt = refund.ProcessedAt,
                Notes = refund.Notes,
                ExternalTransactionId = refund.ExternalTransactionId
            };

            // Get violations for this order
            var violations = await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                .Where(rv => rv.OrderItem.OrderId == refund.OrderId)
                .Select(rv => new ViolationSummary
                {
                    ViolationId = rv.ViolationId,
                    ViolationType = rv.ViolationType.ToString(),
                    Description = rv.Description,
                    PenaltyAmount = rv.PenaltyAmount
                })
                .ToListAsync();

            detail.Violations = violations;

            // Get customer bank info (prefer primary account)
            var bankAccount = await _context.BankAccounts
                .Where(ba => ba.UserId == refund.CustomerId)
                .OrderByDescending(ba => ba.IsPrimary) // Primary account first
                .ThenBy(ba => ba.Id) // Then by creation order
                .FirstOrDefaultAsync();

            if (bankAccount != null)
            {
                detail.BankInfo = new CustomerBankInfo
                {
                    BankAccountId = bankAccount.Id, // Include BankAccount ID for processing
                    BankName = bankAccount.BankName,
                    AccountNumber = bankAccount.AccountNumber,
                    AccountHolderName = bankAccount.AccountHolderName,
                    RoutingNumber = bankAccount.RoutingNumber
                };
            }

            return detail;
        }

        public async Task<IEnumerable<DepositRefundDto>> GetCustomerRefundsAsync(Guid customerId)
        {
            var refunds = await _context.DepositRefunds
                .AsNoTracking()
                .Include(dr => dr.Customer)
                    .ThenInclude(c => c.Profile)
                .Include(dr => dr.RefundBankAccount)
                .Where(dr => dr.CustomerId == customerId)
                .OrderByDescending(dr => dr.CreatedAt)
                .ToListAsync();

            var result = refunds.Select(dr => new DepositRefundDto
            {
                Id = dr.Id,
                RefundCode = $"RF-{dr.Id.ToString().Substring(0, 8).ToUpper()}",
                CreatedAt = dr.CreatedAt,
                CustomerId = dr.CustomerId,
                CustomerName = dr.Customer.Profile != null ? dr.Customer.Profile.FullName : "N/A",
                CustomerEmail = dr.Customer.Email,
                OrderId = dr.OrderId,
                OrderCode = $"ORD-{dr.OrderId.ToString().Substring(0, 8).ToUpper()}",
                OriginalDepositAmount = dr.OriginalDepositAmount,
                TotalPenaltyAmount = dr.TotalPenaltyAmount,
                RefundAmount = dr.RefundAmount,
                RefundBankAccountId = dr.RefundBankAccountId,
                Status = dr.Status,
                Notes = dr.Notes,
                ProcessedByAdminId = dr.ProcessedByAdminId,
                ProcessedAt = dr.ProcessedAt,
                ExternalTransactionId = dr.ExternalTransactionId,
                // Bank info from RefundBankAccount (if processed) or primary bank account
                CustomerBankName = dr.RefundBankAccount != null ? dr.RefundBankAccount.BankName : null,
                CustomerAccountNumber = dr.RefundBankAccount != null ? dr.RefundBankAccount.AccountNumber : null,
                CustomerAccountHolderName = dr.RefundBankAccount != null ? dr.RefundBankAccount.AccountHolderName : null
            }).ToList();

            // For refunds that don't have RefundBankAccount yet, get customer's primary bank account
            foreach (var refund in result.Where(r => string.IsNullOrEmpty(r.CustomerBankName)))
            {
                var primaryBank = await _context.BankAccounts
                    .Where(ba => ba.UserId == refund.CustomerId)
                    .OrderByDescending(ba => ba.IsPrimary)
                    .ThenBy(ba => ba.Id)
                    .FirstOrDefaultAsync();

                if (primaryBank != null)
                {
                    refund.CustomerBankName = primaryBank.BankName;
                    refund.CustomerAccountNumber = primaryBank.AccountNumber;
                    refund.CustomerAccountHolderName = primaryBank.AccountHolderName;
                }
            }

            return result;
        }

        public async Task<DepositRefund> CreateRefundRequestAsync(DepositRefund refund)
        {
            _context.DepositRefunds.Add(refund);
            await _context.SaveChangesAsync();
            return refund;
        }

        public async Task<bool> ApproveRefundAsync(Guid refundId, Guid adminId, Guid? bankAccountId, string? notes, string? externalTransactionId = null)
        {
            var refund = await _context.DepositRefunds.FindAsync(refundId);
            if (refund == null || refund.Status != TransactionStatus.initiated)
                return false;

            refund.Status = TransactionStatus.completed;
            refund.RefundBankAccountId = bankAccountId;
            refund.Notes = notes;
            refund.ExternalTransactionId = externalTransactionId;
            refund.ProcessedByAdminId = adminId;
            refund.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectRefundAsync(Guid refundId, Guid adminId, string? notes)
        {
            var refund = await _context.DepositRefunds.FindAsync(refundId);
            if (refund == null || refund.Status != TransactionStatus.initiated)
                return false;

            refund.Status = TransactionStatus.failed;
            refund.Notes = notes;
            refund.ProcessedByAdminId = adminId;
            refund.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReopenRefundAsync(Guid refundId)
        {
            var refund = await _context.DepositRefunds.FindAsync(refundId);
            if (refund == null || refund.Status != TransactionStatus.failed)
                return false;

            // Reset to initiated status so admin can process again
            refund.Status = TransactionStatus.initiated;
            refund.ProcessedByAdminId = null;
            refund.ProcessedAt = null;
            refund.ExternalTransactionId = null;
            // Keep the Notes for reference (admin can see why it was rejected before)

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetPendingRefundCountAsync()
        {
            return await _context.DepositRefunds
                .CountAsync(dr => dr.Status == TransactionStatus.initiated);
        }
    }
}

