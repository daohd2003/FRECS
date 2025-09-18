using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Extensions;
using System.Text;
using System.Text.Json;

namespace ShareItFE.Pages
{
    public class AuthModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public string GoogleClientId { get; private set; } = string.Empty;
        public string FacebookAppId { get; private set; } = string.Empty;

        public AuthModel(HttpClient httpClient, ILogger<AuthModel> logger, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
            GoogleClientId = _configuration["GoogleClientSettings:ClientId"]
                             ?? throw new InvalidOperationException("GoogleClientSettings:ClientId không được cấu hình.");
            FacebookAppId = _configuration["Facebook:AppId"] ?? string.Empty;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;
        [BindProperty] public string Name { get; set; } = string.Empty;
        [BindProperty] public bool IsLogin { get; set; } = true;
        [BindProperty] public bool RememberMe { get; set; } = false;

        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        private string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public void OnGet()
        {
            if (TempData["SuccessMessage"] is string successMsg)
                SuccessMessage = successMsg;

            if (TempData["ErrorMessage"] is string errorMsg)
                ErrorMessage = errorMsg;

            if (TempData["IsLoginState"] is bool isLoginState)
                IsLogin = isLoginState;
        }

        public async Task<IActionResult> OnPostLogin()
        {
            IsLogin = true;

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                TempData["IsLoginState"] = true;
                return Page();
            }

            var loginRequest = new LoginRequestDto { Email = Email, Password = Password, RememberMe = RememberMe };
            var content = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResponse?.Data != null)
                    {
                        var cookieExpiry = RememberMe ? DateTimeOffset.UtcNow.AddMinutes(10080) : DateTimeOffset.UtcNow.AddMinutes(120);
                        HttpContext.Response.Cookies.Append("AccessToken", apiResponse.Data.Token, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = cookieExpiry
                        });
                        HttpContext.Response.Cookies.Append("RefreshToken", apiResponse.Data.RefreshToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = apiResponse.Data.RefreshTokenExpiryTime
                        });

                        return RedirectToPage("/Index");
                    }

                    ErrorMessage = apiResponse?.Message ?? "Login failed.";
                }
                else
                {
                    // Deserialize error response
                    var error = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, string[]>>>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (error?.Data != null && error.Data.Any())
                    {
                        ErrorMessage = string.Join(" ", error.Data.SelectMany(kvp => kvp.Value));
                    }
                    else
                    {
                        ErrorMessage = error?.Message ?? "Login failed due to server error.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login.");
                ErrorMessage = "An unexpected error occurred. Please try again later.";
            }

            TempData["IsLoginState"] = true;
            TempData.AddErrorToast(ErrorMessage);
            return Page();
        }

        public async Task<IActionResult> OnPostRegister()
        {
            IsLogin = false;

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Full Name, Email, and Password are required.";
                TempData["IsLoginState"] = false;
                return Page();
            }

            var registerRequest = new RegisterRequest
            {
                Email = Email,
                Password = Password,
                FullName = Name
            };
            var content = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/register", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    TempData["SuccessMessage"] = apiResponse?.Message ?? "Registration successful! Please check your email to verify your account.";
                    TempData["IsLoginState"] = true;
                    return RedirectToPage("/Auth");
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, string[]>>>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (error?.Data != null && error.Data.Any())
                    {
                        ErrorMessage = string.Join(" ", error.Data.SelectMany(kvp => kvp.Value));
                    }
                    else
                    {
                        ErrorMessage = error?.Message ?? "Registration failed.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration.");
                ErrorMessage = "An unexpected error occurred. Please try again later.";
            }

            TempData["IsLoginState"] = false;
            TempData["ErrorMessage"] = ErrorMessage;
            return Page();
        }

        public async Task<IActionResult> OnPostGoogleLoginAsync([FromForm] GoogleLoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                TempData.AddErrorToast("Google token is invalid.");
                return RedirectToPage();
            }

            try
            {
                // 1. Tạo request body để gửi đến API backend của bạn
                var apiRequest = new { idToken = request.IdToken };
                var content = new StringContent(JsonSerializer.Serialize(apiRequest), System.Text.Encoding.UTF8, "application/json");

                // 2. Gọi đến API backend để xác thực token và đăng nhập
                // Thay thế "/auth/google-login" bằng endpoint thực tế của bạn
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/google-login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // 3. Xử lý khi đăng nhập thành công (tương tự như đăng nhập thường)
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResponse?.Data != null)
                    {
                        // Lưu AccessToken và RefreshToken vào cookie
                        Response.Cookies.Append("AccessToken", apiResponse.Data.Token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });
                        Response.Cookies.Append("RefreshToken", apiResponse.Data.RefreshToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });

                        return RedirectToPage("/Index"); // Chuyển hướng đến trang chủ
                    }
                }

                // 4. Xử lý khi đăng nhập thất bại
                var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                TempData["ErrorMessage"] = errorResponse?.Message ?? "Google login failed.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during Google login.");
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostFacebookLoginAsync([FromForm] FacebookLoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken))
            {
                TempData["ErrorMessage"] = "Facebook token is invalid.";
                return RedirectToPage();
            }

            try
            {
                var apiRequest = new { accessToken = request.AccessToken };
                var content = new StringContent(JsonSerializer.Serialize(apiRequest), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/facebook-login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (apiResponse?.Data != null)
                    {
                        Response.Cookies.Append("AccessToken", apiResponse.Data.Token, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(30) });
                        Response.Cookies.Append("RefreshToken", apiResponse.Data.RefreshToken, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = apiResponse.Data.RefreshTokenExpiryTime });
                        return RedirectToPage("/Index");
                    }
                }

                TempData["ErrorMessage"] = "Facebook login failed.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during Facebook login.");
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return RedirectToPage();
            }
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Response.Cookies.Delete("AccessToken");
            HttpContext.Response.Cookies.Delete("RefreshToken");

            TempData.AddSuccessToast("You have been successfully logged out.");
            return RedirectToPage("/Auth");
        }
    }
}