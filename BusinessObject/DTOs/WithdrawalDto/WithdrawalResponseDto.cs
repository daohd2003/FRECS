using System;

namespace BusinessObject.DTOs.WithdrawalDto
{
    /// <summary>
    /// DTO for withdrawal request response
    /// </summary>
    public class WithdrawalResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public Guid BankAccountId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolderName { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
        public string? Notes { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessedByAdminName { get; set; }
        public string? RejectionReason { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string? AdminNotes { get; set; }
    }
}

