using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class Feedback
    {
        [Key]
        public Guid Id { get; set; }  // ID duy nhất cho mỗi đánh giá

        [Required]
        public Guid CustomerId { get; set; }  // Người đánh giá

        [Required]
        public Guid ProductId { get; set; }  // Sản phẩm được đánh giá

        [ForeignKey(nameof(CustomerId))]
        public User Customer { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }  // Điểm đánh giá từ 1 đến 5

        [MaxLength(1000)]
        public string? Comment { get; set; }  // Nhận xét (có thể null nếu chỉ đánh giá sao)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // Ngày tạo
    }
}
