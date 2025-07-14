using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Customer
{
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
            // 1. Lấy URL từ configuration
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

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
