using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class Message
    {
        [Key]
        public Guid Id { get; set; }  // ID duy nhất cho mỗi tin nhắn

        [Required]
        public Guid ConversationId { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation? Conversation { get; set; }

        public Guid? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        [Required]
        public Guid SenderId { get; set; }  // ID người gửi

        [Required]
        public Guid ReceiverId { get; set; }  // ID người nhận

        [ForeignKey(nameof(SenderId))]
        public User Sender { get; set; }  // Thông tin người gửi

        [ForeignKey(nameof(ReceiverId))]
        public User Receiver { get; set; }  // Thông tin người nhận

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; }  // Nội dung tin nhắn (tối đa 1000 ký tự để tránh spam)

        public DateTime SentAt { get; set; } = DateTime.UtcNow;  // Thời điểm gửi

        public bool IsRead { get; set; } = false;  // Đã đọc hay chưa
    }
}
