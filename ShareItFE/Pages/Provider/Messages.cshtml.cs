using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Provider
{
    [Authorize(Roles = "provider")]
    public class MessagesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public MessagesModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string? CurrentUserId { get; private set; }
        public string? AccessToken { get; private set; }
        public string? ApiBaseUrl { get; private set; }
        public string? SignalRRootUrl { get; private set; }

        public async Task OnGetAsync()
        {
            // 1. Lấy URL từ configuration
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            SignalRRootUrl = _configuration.GetApiRootUrl(_environment);

            // 2. Kiểm tra để đảm bảo các giá trị không bị null
            if (string.IsNullOrEmpty(ApiBaseUrl) || string.IsNullOrEmpty(SignalRRootUrl))
            {
                ApiBaseUrl ??= string.Empty;
                SignalRRootUrl ??= string.Empty;
            }

            CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            AccessToken = await HttpContext.GetTokenAsync("access_token");
        }
    }
}