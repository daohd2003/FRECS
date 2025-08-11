using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace ShareItFE.Pages
{
    public class VerifyEmailModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VerifyEmailModel> _logger;
        private readonly IConfiguration _configuration;

        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        private string ApiBaseUrl => _configuration["ApiSettings:BaseUrl"];


        public VerifyEmailModel(HttpClient httpClient, ILogger<VerifyEmailModel> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
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
                // Gọi API backend của bạn để xác minh email
                // Chú ý: Backend của bạn đang là [HttpGet], nên ở đây chúng ta sẽ gọi GET
                // Nếu API của bạn là POST, bạn cần tạo một DTO và gửi nó qua PostAsJsonAsync hoặc tương tự.
                // Dựa trên code API bạn cung cấp, nó là GET.

                var requestUrl = $"{ApiBaseUrl}/auth/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
                _logger.LogInformation($"Attempting to verify email with URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    Message = apiResponse?.Message ?? "Email verified successfully!";
                    IsSuccess = true;
                    _logger.LogInformation($"Email '{email}' successfully verified. API Response: {responseContent}");
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

            return Page(); // Trả về trang Razor để hiển thị thông báo
        }
    }
}
