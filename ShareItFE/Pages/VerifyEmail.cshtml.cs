using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using ShareItFE.Extensions;

namespace ShareItFE.Pages
{
    public class VerifyEmailModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VerifyEmailModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        private string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);


        public VerifyEmailModel(HttpClient httpClient, ILogger<VerifyEmailModel> logger, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<IActionResult> OnGetAsync(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                Message = "Invalid verification link. Missing email or token.";
                IsSuccess = false;
                return Page();
            }

            try
            {
                var requestUrl = $"{ApiBaseUrl}/auth/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
                _logger.LogInformation($"Attempting to verify email with URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Backend now returns TokenResponseDto instead of just string message
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (apiResponse?.Data != null)
                    {
                        // Set authentication cookies (same as login flow)
                        HttpContext.Response.Cookies.Append("AccessToken", apiResponse.Data.Token, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.UtcNow.AddMinutes(120) // Default 2 hours
                        });

                        HttpContext.Response.Cookies.Append("RefreshToken", apiResponse.Data.RefreshToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = apiResponse.Data.RefreshTokenExpiryTime
                        });

                        Message = !string.IsNullOrEmpty(apiResponse?.Message) 
                            ? apiResponse.Message 
                            : "Email verified successfully! You are now logged in.";
                        IsSuccess = true;
                        _logger.LogInformation($"Email '{email}' successfully verified and user automatically logged in. API Response: {responseContent}");
                    }
                    else
                    {
                        Message = "Email verification succeeded but login failed. Please try logging in manually.";
                        IsSuccess = false;
                    }
                }
                else
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    Message = apiResponse?.Message ?? "Email verification failed. Please try again or contact support.";
                    IsSuccess = false;
                    _logger.LogWarning($"Email verification failed for '{email}'. Status: {response.StatusCode}. API Response: {responseContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Message = "Could not connect to the verification server. Please try again later.";
                IsSuccess = false;
                _logger.LogError(ex, "HTTP request failed during email verification for email: {Email}", email);
            }
            catch (Exception ex)
            {
                Message = "An unexpected error occurred during email verification.";
                IsSuccess = false;
                _logger.LogError(ex, "Unhandled exception during email verification for email: {Email}", email);
            }

            return Page();
        }
    }
}
