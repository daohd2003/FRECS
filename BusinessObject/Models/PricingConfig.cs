using System.ComponentModel.DataAnnotations;

namespace BusinessObject.Models
{
    public class PricingConfig
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ConfigKey { get; set; } = string.Empty; // e.g., "RENTAL_DISCOUNT_RATE"

        [Required]
        [MaxLength(500)]
        public string ConfigValue { get; set; } = string.Empty; // e.g., "8" for 8%

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Required]
        public Guid UpdatedByAdminId { get; set; }
        public User? UpdatedByAdmin { get; set; }
    }
}
