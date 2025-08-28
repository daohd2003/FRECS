using Microsoft.Extensions.Configuration;

namespace ShareItAPI.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string GetFrontendBaseUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"FrontendSettings:{environment.EnvironmentName}:BaseUrl"] ?? "https://localhost:7045";
        }

        public static string GetVnpayCallbackUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"Vnpay:{environment.EnvironmentName}:CallbackUrl"] ?? "https://localhost:7256/api/payment/Vnpay/Callback";
        }

        public static string GetOpenAIBaseAppUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"OpenAI:{environment.EnvironmentName}:BaseAppUrl"] ?? "https://localhost:7045";
        }
    }
}
