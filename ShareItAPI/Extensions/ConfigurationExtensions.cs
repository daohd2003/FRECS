using Microsoft.Extensions.Configuration;

namespace ShareItAPI.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string GetEnvironmentConfig(this IConfiguration configuration, string sectionKey, string fallbackValue = "", IWebHostEnvironment? environment = null)
        {
            var envName = environment?.EnvironmentName ?? "Development";
            var envSpecificValue = configuration[$"{sectionKey}:{envName}"];
            return envSpecificValue ?? fallbackValue;
        }

        public static string GetFrontendBaseUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("FrontendSettings:BaseUrl", "https://localhost:7045", environment);
        }

        public static string GetVnpayCallbackUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("Vnpay:CallbackUrl", "https://localhost:7256/api/payment/Vnpay/Callback", environment);
        }

        public static string GetOpenAIBaseUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("OpenAI:BaseAppUrl", "https://localhost:7045", environment);
        }
    }
}
