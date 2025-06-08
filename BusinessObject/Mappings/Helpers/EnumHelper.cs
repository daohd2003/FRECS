using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings.Helpers
{
    public static class EnumHelper
    {
        /// <summary>
        /// Chuyển đổi chuỗi thành giá trị enum tương ứng (case-insensitive). Nếu thất bại, trả về giá trị mặc định.
        /// </summary>
        public static TEnum ParseOrDefault<TEnum>(string value, TEnum defaultValue = default) where TEnum : struct, Enum
        {
            return Enum.TryParse<TEnum>(value, true, out var result)
                ? result
                : defaultValue;
        }
    }
}
