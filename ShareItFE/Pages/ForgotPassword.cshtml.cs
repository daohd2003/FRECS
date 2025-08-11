using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text;

namespace ShareItFE.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ForgotPasswordModel> _logger;
        private readonly IConfiguration _configuration;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        private string ApiBaseUrl => _configuration["ApiSettings:BaseUrl"];

        public ForgotPasswordModel(HttpClient httpClient, ILogger<ForgotPasswordModel> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public void OnGet()
        {
            // Retrieve messages from TempData if redirected
            if (TempData["SuccessMessage"] is string successMsg)
            {
                SuccessMessage = successMsg;
            }
            if (TempData["ErrorMessage"] is string errorMsg)
            {
                ErrorMessage = errorMsg;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Basic client-side validation failed (e.g., empty email)
                ErrorMessage = "Please enter a valid email address.";
                return Page();
            }

            // Define the DTO for the backend request
            var forgotPasswordRequest = new
            {
                Email = Email // Ensure this matches your backend's ForgotPasswordRequest DTO structure
            };
            var content = new StringContent(JsonSerializer.Serialize(forgotPasswordRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/forgot-password", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    SuccessMessage = apiResponse?.Message ?? "If an account with that email exists, a password reset link has been sent.";
                    _logger.LogInformation($"Forgot password request successful for {Email}. Message: {SuccessMessage}");
                    TempData["SuccessMessage"] = SuccessMessage; // Store for redirect
                    return RedirectToPage("/ForgotPassword"); // Redirect to clear form and show message
                }
                else
                {
                    var apiError = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = apiError?.Message ?? "Failed to send password reset email. Please try again.";
                    _logger.LogWarning($"Forgot password request failed for {Email}. Status: {response.StatusCode}. Error: {ErrorMessage}");
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to connect to the authentication server. Please try again later.";
                _logger.LogError(ex, "HTTP request failed during forgot password for {Email}.", Email);
            }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred.";
                _logger.LogError(ex, "Unhandled exception during forgot password for {Email}.", Email);
            }

            return Page();
        }
    }
}
