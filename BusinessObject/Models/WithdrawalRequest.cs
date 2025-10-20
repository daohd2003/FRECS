
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class WithdrawalRequest
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ProviderId { get; set; }

        [ForeignKey(nameof(ProviderId))]
        public User Provider { get; set; }

        [Required]
        public Guid BankAccountId { get; set; }

        [ForeignKey(nameof(BankAccountId))]
        public BankAccount BankAccount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,0)")]
        public decimal Amount { get; set; }

        [Required]
        public WithdrawalStatus Status { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public Guid? ProcessedByAdminId { get; set; }

        [ForeignKey(nameof(ProcessedByAdminId))]
        public User? ProcessedByAdmin { get; set; }
        
        [MaxLength(1000)]
        public string? RejectionReason { get; set; }

        [MaxLength(255)]
        public string? ExternalTransactionId { get; set; }

        [MaxLength(1000)]
        public string? AdminNotes { get; set; }
    }
}
