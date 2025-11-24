using BusinessObject.Enums;
using System;

namespace BusinessObject.DTOs.TransactionDto
{
    /// <summary>
    /// DTO cho Transaction Management - hiển thị thông tin giao dịch đầy đủ
    /// </summary>
    public class TransactionManagementDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        
        // Phân loại giao dịch
        public TransactionCategory Category { get; set; }
        public string CategoryDisplay { get; set; }
        
        // Thông tin số tiền
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
        public string StatusDisplay { get; set; }
        
        // Thông tin customer
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        
        // Thông tin provider (nếu có)
        public Guid? ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string ProviderEmail { get; set; }
        
        // Thông tin order liên quan (nếu có)
        public Guid? OrderId { get; set; }
        public string OrderCode { get; set; }
        
        // Chi tiết giao dịch
        public string PaymentMethod { get; set; }
        public string Content { get; set; }
        public string Notes { get; set; }
        
        // Thông tin chi tiết cho từng loại giao dịch
        public TransactionDetailInfo DetailInfo { get; set; }
    }

    /// <summary>
    /// Thông tin chi tiết bổ sung cho từng loại giao dịch
    /// </summary>
    public class TransactionDetailInfo
    {
        // Cho DepositRefund
        public decimal? OriginalDepositAmount { get; set; }
        public decimal? TotalPenaltyAmount { get; set; }
        public decimal? RefundAmount { get; set; }
        public string RefundBankAccountInfo { get; set; }
        public string ExternalTransactionId { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string ProcessedByAdminName { get; set; }
        
        // Cho WithdrawalRequest
        public decimal? WithdrawalAmount { get; set; }
        public string WithdrawalBankAccountInfo { get; set; }
        public DateTime? RequestDate { get; set; }
        public string RejectionReason { get; set; }
        public string AdminNotes { get; set; }
        
        // Cho Order (Purchase/Rental)
        public decimal? TotalOrderAmount { get; set; }
        public decimal? SecurityDeposit { get; set; }
        public TransactionType? OrderType { get; set; }
        public int? TotalItems { get; set; }
    }
}
