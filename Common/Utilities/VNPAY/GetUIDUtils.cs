using System.Text.RegularExpressions;

namespace Common.Utilities.VNPAY
{
    namespace Common.Utilities.VNPAY
    {
        public static class GetUIDUtils
        {
            /// <summary>
            /// Trích xuất CustomerId (Guid) từ chuỗi mô tả có định dạng "UID:{guid}"
            /// </summary>
            public static Guid ExtractCustomerId(string orderDescription)
            {
                if (string.IsNullOrWhiteSpace(orderDescription))
                    return Guid.Empty;

                try
                {
                    var match = Regex.Match(orderDescription, @"UID:([a-fA-F0-9\-]{36})");
                    if (match.Success && Guid.TryParse(match.Groups[1].Value, out Guid customerId))
                    {
                        return customerId;
                    }
                }
                catch { }

                return Guid.Empty;
            }

            /// <summary>
            /// Trích xuất OrderId (Guid) từ chuỗi mô tả có định dạng "OID:{guid}"
            /// </summary>
            public static Guid ExtractOrderId(string orderDescription)
            {
                if (string.IsNullOrWhiteSpace(orderDescription))
                    return Guid.Empty;

                try
                {
                    var match = Regex.Match(orderDescription, @"OID:([a-fA-F0-9\-]{36})");
                    if (match.Success && Guid.TryParse(match.Groups[1].Value, out Guid orderId))
                    {
                        return orderId;
                    }
                }
                catch { }

                return Guid.Empty;
            }
        }
    }
}
