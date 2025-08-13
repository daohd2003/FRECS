using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
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

        public async Task<IActionResult> OnGetAsync()
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

            ApiBaseUrl ??= string.Empty;
            SignalRRootUrl ??= string.Empty;

            CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            AccessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(AccessToken))
            {
                return RedirectToPage("/Auth");
            }

            return Page();
        }
    }
}


