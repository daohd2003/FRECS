using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "staff,admin")]
    public class FeedbackManagementModel : PageModel
    {
        public string? AccessToken { get; set; }
        
        public void OnGet()
        {
            // Get token from cookie
            AccessToken = Request.Cookies["AccessToken"];
        }
    }
}
