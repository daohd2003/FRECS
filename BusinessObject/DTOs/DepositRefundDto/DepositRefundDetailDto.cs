using BusinessObject.Enums;
using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.DepositRefundDto
{
    /// <summary>
    /// DTO chi tiết cho modal xem refund request
    /// </summary>
    public class DepositRefundDetailDto
    {
        public Guid Id { get; set; }
        
        public string RefundCode { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        // Customer Info
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        
        // Order Info
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime? OrderReturnedDate { get; set; }
        
        // Amount Breakdown
        public decimal OriginalDepositAmount { get; set; }
        public decimal TotalPenaltyAmount { get; set; }
        public decimal RefundAmount { get; set; }
        
        // Penalty Details (nếu có)
        public List<ViolationSummary> Violations { get; set; } = new();
        
        // Payment Info - Bank Account used for refund
        public Guid? RefundBankAccountId { get; set; }
        public TransactionStatus Status { get; set; }
        public string StatusDisplay => Status.ToString();
        
        // Bank Info
        public CustomerBankInfo? BankInfo { get; set; }
        
        // Processing Info
        public Guid? ProcessedByAdminId { get; set; }
        public string? ProcessedByAdminName { get; set; }
        public DateTime? ProcessedAt { get; set; }
        
        // Notes
        public string? Notes { get; set; }
        
        // External Transaction Info
        public string? ExternalTransactionId { get; set; }
    }
    
    public class ViolationSummary
    {
        public Guid ViolationId { get; set; }
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PenaltyAmount { get; set; }
    }
    
    public class CustomerBankInfo
    {
        public Guid BankAccountId { get; set; } // ID của BankAccount
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;
        public string? RoutingNumber { get; set; }
    }
}

