using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.NotificationDto;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages
{
    public class NotificationsModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public NotificationsModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);
        public string AccessToken { get; set; }
        public string UserId { get; set; }
        public string UserRole { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToPage("/Auth");
            }

            UserId = userIdClaim.Value;
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "customer";
            AccessToken = Request.Cookies["AccessToken"];

            return Page();
        }
    }
}

