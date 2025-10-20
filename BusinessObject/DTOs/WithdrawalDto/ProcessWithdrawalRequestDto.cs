using System;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.WithdrawalDto
{
    /// <summary>
    /// DTO for admin to process (approve/reject) a withdrawal request
    /// </summary>
    public class ProcessWithdrawalRequestDto
    {
        [Required(ErrorMessage = "Withdrawal Request ID is required.")]
        public Guid WithdrawalRequestId { get; set; }

        [Required(ErrorMessage = "Status is required (Completed or Rejected).")]
        public string Status { get; set; } // "Completed" or "Rejected"

        [MaxLength(1000, ErrorMessage = "Rejection reason cannot exceed 1000 characters.")]
        public string? RejectionReason { get; set; }

        [MaxLength(255, ErrorMessage = "External Transaction ID cannot exceed 255 characters.")]
        public string? ExternalTransactionId { get; set; }

        [MaxLength(1000, ErrorMessage = "Admin notes cannot exceed 1000 characters.")]
        public string? AdminNotes { get; set; }
    }
}

