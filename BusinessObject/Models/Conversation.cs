using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class Conversation
    {
        [Key]
        public Guid Id { get; set; }  // ID duy nhất của cuộc hội thoại

        [Required]
        public Guid User1Id { get; set; }  // ID người dùng đầu tiên (bên A)

        [Required]
        public Guid User2Id { get; set; }  // ID người dùng thứ hai (bên B)

        [ForeignKey(nameof(User1Id))]
        public User User1 { get; set; }

        [ForeignKey(nameof(User2Id))]
        public User User2 { get; set; }

        public Guid? LastMessageId { get; set; }  // ID tin nhắn cuối cùng (nullable để tránh lỗi khi tạo lần đầu)

        [ForeignKey(nameof(LastMessageId))]
        public Message? LastMessage { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  // Thời điểm cập nhật cuối (ví dụ: có tin mới)
    }
}
