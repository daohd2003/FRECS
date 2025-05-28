using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Enums
{
    public enum UserRole
    {
        /// <summary>
        /// Người dùng thông thường, có thể thuê quần áo
        /// </summary>
        Customer,

        /// <summary>
        /// Nhân viên quản lý đơn hàng, sản phẩm
        /// </summary>
        Staff,

        /// <summary>
        /// Quản trị viên hệ thống
        /// </summary>
        Admin,

        /// <summary>
        /// Chủ hệ thống/platform
        /// </summary>
        SuperAdmin
    }
}
