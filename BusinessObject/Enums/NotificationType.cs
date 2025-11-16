using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Enums
{
    /// <summary>
    /// Loại thông báo được gửi đến người dùng.
    /// </summary>
    public enum NotificationType
    {
        order,      // Thông báo liên quan đến đơn hàng
        message,    // Thông báo tin nhắn hoặc chat
        system,     // Thông báo hệ thống chung, như cập nhật hoặc cảnh báo
        content_violation  // Thông báo vi phạm nội dung sản phẩm
    }
}
