using BusinessObject.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObject.Models
{
    public class ProviderApplication
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string BusinessName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? TaxId { get; set; }

        [MaxLength(255)]
        public string? ContactPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? IdCardFrontImageUrl { get; set; }

        [MaxLength(500)]
        public string? IdCardBackImageUrl { get; set; }

        [Required]
        public ProviderApplicationStatus Status { get; set; } = ProviderApplicationStatus.pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public Guid? ReviewedByAdminId { get; set; }

        [ForeignKey(nameof(ReviewedByAdminId))]
        public User? ReviewedByAdmin { get; set; }

        [MaxLength(500)]
        public string? ReviewComment { get; set; }
    }
}


