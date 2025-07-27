using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.NotificationDto
{
    public class NotificationResponse
    {
        /// <summary>
        /// ID của thông báo.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Nội dung tin nhắn của thông báo.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Trạng thái đã đọc hay chưa.
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// Thời gian tạo thông báo.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Loại thông báo (ví dụ: OrderStatusChanged, NewMessage, Promotion).
        /// </summary>
        public NotificationType Type { get; set; }

        public Guid? OrderId { get; set; }
    }
}
