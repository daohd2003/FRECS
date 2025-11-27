using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Customer
{
    public class TryOnGalleryModel : PageModel
    {
        private readonly ILogger<TryOnGalleryModel> _logger;

        public TryOnGalleryModel(ILogger<TryOnGalleryModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToPage("/Auth");
            }

            return Page();
        }
    }
}
