using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "admin,staff")]
    public class ProviderApplicationManagementModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ProviderApplicationManagementModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string ApiBaseUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string CurrentUserRole { get; set; } = string.Empty;

        public void OnGet()
        {
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            AccessToken = HttpContext.Request.Cookies["AccessToken"] ?? string.Empty;
            CurrentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        }
    }
}

