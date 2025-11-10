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

        [MaxLength(255)]
        public string? TaxId { get; set; }

        [MaxLength(255)]
        public string? ContactPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IFormFile? IdCardFrontImage { get; set; }
        public IFormFile? IdCardBackImage { get; set; }
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


