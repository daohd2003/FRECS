using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin,staff")]
    public class UserManagementModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public UserManagementModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string? CurrentUserId { get; private set; }
        public string? CurrentUserRole { get; private set; }
        public string? AccessToken { get; private set; }
        public string? ApiBaseUrl { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Get current user information
            CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            CurrentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            // Get access token from cookies
            AccessToken = HttpContext.Request.Cookies["AccessToken"];
            
            // Get API base URL
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);

            // Check if user has access
            if (string.IsNullOrEmpty(CurrentUserRole) || 
                (CurrentUserRole != "admin" && CurrentUserRole != "staff"))
            {
                return RedirectToPage("/Auth");
            }

            if (string.IsNullOrEmpty(AccessToken))
            {
                return RedirectToPage("/Auth");
            }

            return Page();
        }
    }
}
