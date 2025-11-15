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

        // Provider Type: Individual (12 digits) or Business (10 digits)
        // TODO: Convert to enum after migration
        [Required]
        [MaxLength(20)]
        public string ProviderType { get; set; } = "Individual";

        // ID Card Images (Required for both Individual and Business)
        [MaxLength(500)]
        public string? IdCardFrontImageUrl { get; set; }

        [MaxLength(500)]
        public string? IdCardBackImageUrl { get; set; }

        // Business License (Required for Business with 10-digit Tax ID)
        [MaxLength(500)]
        public string? BusinessLicenseImageUrl { get; set; }

        // CCCD Verification Data from FPT.AI eKYC
        public bool CccdVerified { get; set; } = false;

        [MaxLength(50)]
        public string? CccdIdNumber { get; set; }

        [MaxLength(255)]
        public string? CccdFullName { get; set; }

        [MaxLength(50)]
        public string? CccdDateOfBirth { get; set; }

        [MaxLength(50)]
        public string? CccdSex { get; set; }

        [MaxLength(255)]
        public string? CccdAddress { get; set; }

        public double? CccdConfidenceScore { get; set; }

        public DateTime? CccdVerifiedAt { get; set; }

        [MaxLength(1000)]
        public string? CccdVerificationError { get; set; }

        // Face Matching (Compare selfie with CCCD photo)
        public bool FaceMatched { get; set; } = false;

        public double? FaceMatchScore { get; set; }

        [MaxLength(500)]
        public string? SelfieImageUrl { get; set; }

        // Privacy Policy Agreement
        public bool PrivacyPolicyAgreed { get; set; } = false;

        public DateTime? PrivacyPolicyAgreedAt { get; set; }

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


