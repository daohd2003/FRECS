using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Provider
{
    [Authorize(Roles = "provider")]
    public class MessagesModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public MessagesModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string? CurrentUserId { get; private set; }
        public string? AccessToken { get; private set; }
        public string? ApiBaseUrl { get; private set; }
        public string? SignalRRootUrl { get; private set; }

        public async Task OnGetAsync()
        {
            // 1. Lấy URL từ configuration
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

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