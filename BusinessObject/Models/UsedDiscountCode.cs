using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObject.Models
{
    public class UsedDiscountCode
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        [Required]
        public Guid DiscountCodeId { get; set; }
        [ForeignKey(nameof(DiscountCodeId))]
        public DiscountCode DiscountCode { get; set; }

        [Required]
        public Guid OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; }

        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }
}
