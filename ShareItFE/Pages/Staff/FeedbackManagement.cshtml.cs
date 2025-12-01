using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "staff,admin")]
    public class FeedbackManagementModel : PageModel
    {
        public string? AccessToken { get; set; }
        public string CurrentUserRole { get; set; } = string.Empty;
        
        public void OnGet()
        {
            // Get token from cookie
            AccessToken = Request.Cookies["AccessToken"];
            // Get current user role
            CurrentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }
    }
}
