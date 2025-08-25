using Microsoft.Extensions.Configuration;

namespace ShareItFE.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string GetApiBaseUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"ApiSettings:{environment.EnvironmentName}:BaseUrl"] ?? "https://localhost:7256/api";
        }

        public static string GetApiRootUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"ApiSettings:{environment.EnvironmentName}:RootUrl"] ?? "https://localhost:7256";
        }

        public static string GetFrontendBaseUrl(this IConfiguration configuration, IWebHostEnvironment environment)
        {
            return configuration[$"FrontendSettings:{environment.EnvironmentName}:BaseUrl"] ?? "https://localhost:7045";
        }
    }
}
