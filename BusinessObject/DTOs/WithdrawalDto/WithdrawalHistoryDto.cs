using System;

namespace BusinessObject.DTOs.WithdrawalDto
{
    /// <summary>
    /// Simplified DTO for withdrawal history list
    /// </summary>
    public class WithdrawalHistoryDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string BankName { get; set; }
        public string AccountLast4 { get; set; }
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
    }
}

