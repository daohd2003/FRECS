using BusinessObject.Enums;

namespace BusinessObject.DTOs.DiscountCodeDto
{
    public class DiscountCodeDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public DiscountType DiscountType { get; set; }
        public decimal Value { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public int UsedCount { get; set; }
        public DiscountStatus Status { get; set; }
        public DiscountUsageType UsageType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
