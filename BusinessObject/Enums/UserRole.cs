using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Enums
{
    /// <summary>
    /// Vai trò người dùng trong hệ thống
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Người dùng thông thường, có thể thuê quần áo
        /// </summary>
        customer,

        /// <summary>
        /// Nhân viên quản lý đơn hàng, sản phẩm
        /// </summary>
        provider,

        /// <summary>
        /// Quản trị viên hệ thống
        /// </summary>
        admin
    }
}
