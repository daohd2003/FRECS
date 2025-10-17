using System.Net.Sockets;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Common.Utilities.VNPAY
{
    public class NetworkHelper
    {
        /// <summary>
        /// Lấy địa chỉ IP từ HttpContext của API Controller.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetIpAddress(HttpContext context)
        {
            var remoteIpAddress = context.Connection.RemoteIpAddress;

            if (remoteIpAddress != null)
            {
                // Check for forwarded headers first (in case behind proxy/load balancer)
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    var firstIP = forwardedFor.Split(',')[0].Trim();
                    if (System.Net.IPAddress.TryParse(firstIP, out var parsedIP))
                    {
                        return parsedIP.ToString();
                    }
                }

                var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIP))
                {
                    if (System.Net.IPAddress.TryParse(realIP, out var parsedRealIP))
                    {
                        return parsedRealIP.ToString();
                    }
                }

                // For IPv6 localhost, return IPv4 localhost
                if (remoteIpAddress.ToString() == "::1")
                {
                    return "127.0.0.1";
                }

                // If IPv6, try to map to IPv4 if possible, otherwise return as string
                if (remoteIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (remoteIpAddress.IsIPv4MappedToIPv6)
                    {
                        return remoteIpAddress.MapToIPv4().ToString();
                    }
                    // For other IPv6 addresses, return as string without DNS lookup
                    return remoteIpAddress.ToString();
                }

                // For IPv4, return directly without DNS lookup
                return remoteIpAddress.ToString();
            }

            // Fallback IP for localhost/development
            return "127.0.0.1";
        }
    }
}
