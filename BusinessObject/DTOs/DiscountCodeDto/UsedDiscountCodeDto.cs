namespace BusinessObject.DTOs.DiscountCodeDto
{
    public class UsedDiscountCodeDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public Guid DiscountCodeId { get; set; }
        public string DiscountCode { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public DateTime UsedAt { get; set; }
    }
}
