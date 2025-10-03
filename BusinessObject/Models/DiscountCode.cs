using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    public class DiscountCode
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public DiscountType DiscountType { get; set; } = Enums.DiscountType.Percentage;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Value { get; set; }

        [Required]
        public DateTime ExpirationDate { get; set; }

        [Required]
        public int Quantity { get; set; }

        public int UsedCount { get; set; } = 0;

        [Required]
        public DiscountStatus Status { get; set; } = DiscountStatus.Active;

        [Required]
        public DiscountUsageType UsageType { get; set; } = DiscountUsageType.Purchase;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<UsedDiscountCode> UsedDiscountCodes { get; set; } = new List<UsedDiscountCode>();
    }
}
