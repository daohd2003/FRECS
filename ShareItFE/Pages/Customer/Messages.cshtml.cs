using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Customer
{
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

        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Lấy URL từ configuration
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            SignalRRootUrl = _configuration.GetApiRootUrl(_environment);

            // 2. Kiểm tra để đảm bảo các giá trị không bị null
            ApiBaseUrl ??= string.Empty;
            SignalRRootUrl ??= string.Empty;

            // Lấy User ID của người dùng hiện tại từ Claims
            CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Lấy Access Token từ HttpContext.Authentication
            AccessToken = await HttpContext.GetTokenAsync("access_token");

            // 3. Xử lý trường hợp AccessToken bị thiếu
            if (string.IsNullOrEmpty(AccessToken))
            {
                return RedirectToPage("/Auth");
            }

            return Page();
        }
    }
}
