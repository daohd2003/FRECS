using System;

namespace ShareItFE.Utilities
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo DefaultTimeZone;

        static DateTimeHelper()
        {
            // Try to get SEA timezone with fallback
            try
            {
                DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    // Fallback for non-Windows systems
                    DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
                }
                catch (TimeZoneNotFoundException)
                {
                    // Ultimate fallback to UTC+7
                    DefaultTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                        "SEA_Custom", 
                        TimeSpan.FromHours(7), 
                        "SE Asia Standard Time", 
                        "SE Asia Standard Time");
                }
            }
        }

        /// <summary>
        /// Safely converts UTC DateTime to SE Asia timezone with proper error handling
        /// </summary>
        /// <param name="utcDateTime">The UTC DateTime to convert</param>
        /// <returns>DateTime in SE Asia timezone, or original DateTime if conversion fails</returns>
        public static DateTime ToSeAsiaTime(DateTime utcDateTime)
        {
            try
            {
                // Ensure the DateTime is treated as UTC if Kind is Unspecified
                if (utcDateTime.Kind == DateTimeKind.Unspecified)
                {
                    utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
                }
                else if (utcDateTime.Kind == DateTimeKind.Local)
                {
                    // Convert local time to UTC first
                    utcDateTime = utcDateTime.ToUniversalTime();
                }

                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, DefaultTimeZone);
            }
            catch (Exception)
            {
                // Fallback: if conversion fails, add 7 hours manually (UTC+7)
                return utcDateTime.AddHours(7);
            }
        }

        /// <summary>
        /// Safely converts UTC DateTime to SE Asia timezone and formats as string
        /// </summary>
        /// <param name="utcDateTime">The UTC DateTime to convert</param>
        /// <param name="format">The format string (default: "dd/MM/yyyy HH:mm")</param>
        /// <returns>Formatted string in SE Asia timezone</returns>
        public static string ToSeAsiaTimeString(DateTime utcDateTime, string format = "dd/MM/yyyy HH:mm")
        {
            try
            {
                return ToSeAsiaTime(utcDateTime).ToString(format);
            }
            catch (Exception)
            {
                // Fallback: return UTC time with format
                return utcDateTime.ToString(format) + " UTC";
            }
        }

        /// <summary>
        /// Get the current SE Asia timezone for display purposes
        /// </summary>
        /// <returns>TimeZoneInfo for SE Asia, or UTC+7 custom timezone</returns>
        public static TimeZoneInfo GetSeAsiaTimeZone()
        {
            return DefaultTimeZone;
        }
    }
}
