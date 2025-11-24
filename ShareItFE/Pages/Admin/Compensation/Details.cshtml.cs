using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace ShareItFE.Pages.Admin.Compensation
{
    [Authorize(Roles = "admin")]
    public class DetailsModel : PageModel
    {
        public Guid ViolationId { get; set; }

        public IActionResult OnGet(Guid violationId)
        {
            ViolationId = violationId;
            return Page();
        }
    }
}

