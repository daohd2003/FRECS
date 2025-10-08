using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BusinessObject.Validators;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class StaffManagementModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _httpClientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public string? CurrentUserId { get; set; }
        public string? AccessToken { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? SignalRRootUrl { get; set; }

        public StaffManagementModel(
            AuthenticatedHttpClientHelper httpClientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _httpClientHelper = httpClientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            AccessToken = await HttpContext.GetTokenAsync("access_token");
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            SignalRRootUrl = _configuration.GetApiRootUrl(_environment);

            if (string.IsNullOrEmpty(AccessToken))
            {
                return RedirectToPage("/Auth");
            }

            return Page();
        }

        public async Task<IActionResult> OnGetStaffListAsync()
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("/api/staff");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"API call failed with status {response.StatusCode}",
                        error = responseContent
                    });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetStaffByIdAsync(Guid id)
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"/api/staff/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "Staff not found" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostCreateStaffAsync([FromBody] CreateStaffDto staff)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return new JsonResult(new { success = false, message = "Invalid data", errors = ModelState });
                }

                var json = JsonSerializer.Serialize(staff);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.PostAsync("/api/staff", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to create staff", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPutUpdateStaffAsync(Guid id, [FromBody] UpdateStaffDto staff)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return new JsonResult(new { success = false, message = "Invalid data", errors = ModelState });
                }

                var json = JsonSerializer.Serialize(staff);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.PutAsync($"/api/staff/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to update staff", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnDeleteStaffAsync(Guid id)
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"/api/staff/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Staff deleted successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to delete staff", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostSendPasswordResetAsync(Guid id)
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.PostAsync($"/api/staff/{id}/send-password-reset", null);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to send password reset", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateStaffDto
    {
        [Required(ErrorMessage = "Full name is required")]
        [MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email format is invalid")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        [Uppercase]
        [Lowercase]
        [Numeric]
        [SpecialCharacter]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public class UpdateStaffDto
    {
        [Required(ErrorMessage = "Full name is required")]
        [MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email format is invalid")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}

