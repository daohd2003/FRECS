using System;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.WithdrawalDto
{
    /// <summary>
    /// DTO for provider to request a withdrawal/payout
    /// </summary>
    public class WithdrawalRequestDto
    {
        [Required(ErrorMessage = "Bank Account ID is required.")]
        public Guid BankAccountId { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(50000, double.MaxValue, ErrorMessage = "Minimum withdrawal amount is 50,000 VND.")]
        public decimal Amount { get; set; }

        [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }
    }
}

