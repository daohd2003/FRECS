using System;

namespace BusinessObject.Utilities
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo VietnamTimeZone;

        static DateTimeHelper()
        {
            try
            {
                // Try to get Vietnam timezone
                VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    // Fallback for non-Windows systems
                    VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch (TimeZoneNotFoundException)
                {
                    // Ultimate fallback - create custom UTC+7 timezone
                    VietnamTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                        "VietnamCustom", 
                        TimeSpan.FromHours(7), 
                        "Vietnam Standard Time", 
                        "Vietnam Standard Time"
                    );
                }
            }
        }

        /// <summary>
        /// Get current Vietnam time (UTC+7)
        /// </summary>
        /// <returns>Current DateTime in Vietnam timezone</returns>
        public static DateTime GetVietnamTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        }

        /// <summary>
        /// Convert UTC time to Vietnam time
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime to convert</param>
        /// <returns>DateTime in Vietnam timezone</returns>
        public static DateTime ToVietnamTime(DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);
        }

        /// <summary>
        /// Convert Vietnam time to UTC
        /// </summary>
        /// <param name="vietnamDateTime">Vietnam DateTime to convert</param>
        /// <returns>DateTime in UTC</returns>
        public static DateTime ToUtcTime(DateTime vietnamDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(vietnamDateTime, VietnamTimeZone);
        }
    }
}
