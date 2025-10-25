using BusinessObject.Enums;
using System;

namespace BusinessObject.DTOs.DepositRefundDto
{
    /// <summary>
    /// DTO cho hiển thị danh sách refund requests trong admin dashboard
    /// </summary>
    public class DepositRefundDto
    {
        public Guid Id { get; set; }
        
        public string RefundCode { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        // Customer Info
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        
        // Order Info
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        
        // Amount Info
        public decimal OriginalDepositAmount { get; set; }
        public decimal TotalPenaltyAmount { get; set; }
        public decimal RefundAmount { get; set; }
        
        // Payment Info
        public Guid? RefundBankAccountId { get; set; }
        public TransactionStatus Status { get; set; }
        public string StatusDisplay => Status.ToString();
        
        // Bank Info (from Customer's BankAccount)
        public string? CustomerBankName { get; set; }
        public string? CustomerAccountNumber { get; set; }
        public string? CustomerAccountHolderName { get; set; }
        
        // Notes
        public string? Notes { get; set; }
        
        // Admin Processing Info
        public Guid? ProcessedByAdminId { get; set; }
        public string? ProcessedByAdminName { get; set; }
        public DateTime? ProcessedAt { get; set; }
        
        // External Transaction Info
        public string? ExternalTransactionId { get; set; }
    }
}

