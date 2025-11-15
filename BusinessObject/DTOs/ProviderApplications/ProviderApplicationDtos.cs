using System.ComponentModel.DataAnnotations;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Http;

namespace BusinessObject.DTOs.ProviderApplications
{
    public class ProviderApplicationCreateDto
    {
        [Required]
        [MaxLength(255)]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string? TaxId { get; set; }

        [Required(ErrorMessage = "Contact phone is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact phone must be exactly 10 digits")]
        public string? ContactPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Provider Type: Determined by Tax ID length
        // 12 digits = Individual (Cá nhân)
        // 10 digits = Business (Doanh nghiệp)
        // TODO: Convert to enum after migration
        [Required]
        [MaxLength(20)]
        public string ProviderType { get; set; } = "Individual";

        // ID Card Images (Required for BOTH Individual and Business)
        [Required(ErrorMessage = "ID Card Front Image is required")]
        public IFormFile? IdCardFrontImage { get; set; }

        [Required(ErrorMessage = "ID Card Back Image is required")]
        public IFormFile? IdCardBackImage { get; set; }

        // Selfie for Face Matching (Required for Individual with 12-digit Tax ID)
        public IFormFile? SelfieImage { get; set; }

        // Business License (Required for Business with 10-digit Tax ID)
        public IFormFile? BusinessLicenseImage { get; set; }

        // Privacy Policy Agreement (Required for ALL)
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the privacy policy to continue.")]
        public bool PrivacyPolicyAgreed { get; set; } = false;
    }

    public class ProviderApplicationReviewDto
    {
        [Required]
        public Guid ApplicationId { get; set; }

        [Required]
        public ProviderApplicationStatus NewStatus { get; set; }

        [MaxLength(500)]
        public string? ReviewComment { get; set; }
    }
}


