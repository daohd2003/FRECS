using Microsoft.Extensions.Configuration;

namespace ShareItFE.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string GetEnvironmentConfig(this IConfiguration configuration, string sectionKey, string fallbackValue = "", IWebHostEnvironment? environment = null)
        {
            var envName = environment?.EnvironmentName ?? "Development";
            var envSpecificValue = configuration[$"{sectionKey}:{envName}"];
            return envSpecificValue ?? fallbackValue;
        }

        public static string GetApiBaseUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("ApiSettings:BaseUrl", "https://localhost:7256/api", environment);
        }

        public static string GetApiRootUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("ApiSettings:RootUrl", "https://localhost:7256", environment);
        }

        public static string GetFrontendBaseUrl(this IConfiguration configuration, IWebHostEnvironment? environment = null)
        {
            return configuration.GetEnvironmentConfig("FrontendSettings:BaseUrl", "https://localhost:7045", environment);
        }
    }
}
