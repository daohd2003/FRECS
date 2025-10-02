using System.ComponentModel.DataAnnotations;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.DiscountCodeDto
{
    public class UpdateDiscountCodeDto
    {
        [Required(ErrorMessage = "Discount code is required")]
        [StringLength(50, ErrorMessage = "Discount code cannot exceed 50 characters")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        public DiscountType DiscountType { get; set; } = Enums.DiscountType.Percentage;

        [Required(ErrorMessage = "Value is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }

        [Required(ErrorMessage = "Expiration date is required")]
        public DateTime ExpirationDate { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public DiscountStatus Status { get; set; } = DiscountStatus.Active;
    }
}
