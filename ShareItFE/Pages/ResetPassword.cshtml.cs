using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text;

namespace ShareItFE.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ResetPasswordModel> _logger;
        private readonly IConfiguration _configuration;

        [BindProperty(SupportsGet = true)] // Allow binding from query string on GET
        public string Email { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)] // Allow binding from query string on GET
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "New password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;


        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        private string ApiBaseUrl => _configuration["ApiSettings:BaseUrl"];

        public ResetPasswordModel(HttpClient httpClient, ILogger<ResetPasswordModel> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult OnGet()
        {
            // Email and Token are already bound from query string due to SupportsGet = true
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Token))
            {
                ErrorMessage = "Invalid or incomplete password reset link.";
            }

            // Retrieve messages from TempData if redirected
            if (TempData["SuccessMessage"] is string successMsg)
            {
                SuccessMessage = successMsg;
            }
            if (TempData["ErrorMessage"] is string errorMsg)
            {
                ErrorMessage = errorMsg;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Validation errors will be displayed by asp-validation-for spans
                return Page();
            }

            // Define the DTO for the backend request
            var resetPasswordRequest = new
            {
                Email = Email,
                Token = Token,
                NewPassword = NewPassword
            };
            var content = new StringContent(JsonSerializer.Serialize(resetPasswordRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/reset-password", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    SuccessMessage = apiResponse?.Message ?? "Your password has been reset successfully. You can now log in.";
                    _logger.LogInformation($"Password reset successful for {Email}. Message: {SuccessMessage}");
                    TempData["SuccessMessage"] = SuccessMessage; // Store for redirect
                    return RedirectToPage("/Auth"); // Redirect to login page after successful reset
                }
                else
                {
                    var apiError = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = apiError?.Message ?? "Failed to reset password. Please check the link or try again.";
                    _logger.LogWarning($"Password reset failed for {Email}. Status: {response.StatusCode}. Error: {ErrorMessage}");
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to connect to the authentication server. Please try again later.";
                _logger.LogError(ex, "HTTP request failed during password reset for {Email}.", Email);
            }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred.";
                _logger.LogError(ex, "Unhandled exception during password reset for {Email}.", Email);
            }

            return Page();
        }
    }
}
