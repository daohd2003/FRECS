using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    public class Feedback
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public User Customer { get; set; }

        [Required]
        public FeedbackTargetType TargetType { get; set; } // PRODUCT hoặc ORDER

        // ProductId sẽ là null nếu TargetType là ORDER
        public Guid? ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        public Guid? OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        // OrderItemId sẽ là null nếu TargetType là PRODUCT 
        public Guid? OrderItemId { get; set; }
        [ForeignKey(nameof(OrderItemId))]
        public OrderItem? OrderItem { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(1000)]
        public string? ProviderResponse { get; set; } // Nội dung phản hồi
        public DateTime? ProviderResponseAt { get; set; } // Thời điểm phản hồi
        public Guid? ProviderResponseById { get; set; } // ID của Provider/Admin đã phản hồi
        [ForeignKey(nameof(ProviderResponseById))]
        public User? ProviderResponder { get; set; }
    }
}