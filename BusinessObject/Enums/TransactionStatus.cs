using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái của một giao dịch tài chính hoặc thanh toán.
    /// </summary>
    public enum TransactionStatus
    {
        initiated,  // Giao dịch đã được khởi tạo
        completed,  // Giao dịch hoàn thành thành công
        failed      // Giao dịch thất bại hoặc bị từ chối
    }
}
