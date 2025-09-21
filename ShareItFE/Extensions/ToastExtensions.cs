using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace ShareItFE.Extensions
{
    public static class ToastExtensions
    {
        private const string TempDataKey = "ToastMessages";

        public static void AddToast(this ITempDataDictionary tempData, string message, string type = "info", int duration = 5000)
        {
            var toastMessages = GetToastMessages(tempData);
            toastMessages.Add(new ToastMessage
            {
                Message = message,
                Type = type,
                Duration = duration
            });
            tempData[TempDataKey] = JsonSerializer.Serialize(toastMessages);
        }

        public static void AddSuccessToast(this ITempDataDictionary tempData, string message, int duration = 5000)
        {
            AddToast(tempData, message, "success", duration);
        }

        public static void AddErrorToast(this ITempDataDictionary tempData, string message, int duration = 5000)
        {
            AddToast(tempData, message, "error", duration);
        }

        public static void AddInfoToast(this ITempDataDictionary tempData, string message, int duration = 5000)
        {
            AddToast(tempData, message, "info", duration);
        }

        public static void AddWarningToast(this ITempDataDictionary tempData, string message, int duration = 5000)
        {
            AddToast(tempData, message, "warning", duration);
        }

        public static string GetToastMessagesJson(this ITempDataDictionary tempData)
        {
            var json = tempData[TempDataKey]?.ToString();
            if (!string.IsNullOrEmpty(json))
            {
                tempData.Remove(TempDataKey); // Remove after reading to ensure one-time display
                return json;
            }
            return "[]";
        }

        private static List<ToastMessage> GetToastMessages(ITempDataDictionary tempData)
        {
            var json = tempData[TempDataKey]?.ToString();
            if (string.IsNullOrEmpty(json))
                return new List<ToastMessage>();

            try
            {
                return JsonSerializer.Deserialize<List<ToastMessage>>(json) ?? new List<ToastMessage>();
            }
            catch
            {
                return new List<ToastMessage>();
            }
        }
    }

    public class ToastMessage
    {
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info";
        public int Duration { get; set; } = 5000;
    }
}
