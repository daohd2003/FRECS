using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.PricingDto
{
    public class PricingConfigDto
    {
        public Guid Id { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedByAdminName { get; set; } = string.Empty;
    }

    public class UpdatePricingConfigDto
    {
        [Required]
        [MaxLength(100)]
        public string ConfigKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ConfigValue { get; set; } = string.Empty;
    }

    public class ProductPriceDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public int RentCount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsDiscounted { get; set; }
    }
}
