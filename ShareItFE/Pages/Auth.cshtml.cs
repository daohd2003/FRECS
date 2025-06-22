using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text;

namespace ShareItFE.Pages
{
    public class AuthModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthModel> _logger;
        private readonly IConfiguration _configuration;
        public string GoogleClientId { get; private set; } = string.Empty;


        public AuthModel(HttpClient httpClient, ILogger<AuthModel> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            GoogleClientId = _configuration["GoogleClientSettings:ClientId"]
                             ?? throw new InvalidOperationException("GoogleClientSettings:ClientId không được cấu hình.");
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;
        [BindProperty]
        public string Password { get; set; } = string.Empty;
        [BindProperty]
        public string Name { get; set; } = string.Empty;
        [BindProperty]
        public bool IsLogin { get; set; } = true;

        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        // API base URL from appsettings.json
        private string ApiBaseUrl => _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7256/api";

        public void OnGet()
        {
            if (TempData["SuccessMessage"] is string successMsg)
            {
                SuccessMessage = successMsg;
                // Nếu có SuccessMessage, có thể giả định là login thành công hoặc đăng ký thành công
                // Nếu là đăng ký thành công, bạn muốn trở lại form login, nên IsLogin = true là hợp lý
                IsLogin = true;
            }
            if (TempData["ErrorMessage"] is string errorMsg)
            {
                ErrorMessage = errorMsg;
                if (TempData["IsLoginState"] is bool isLoginState)
                {
                    IsLogin = isLoginState; // Lấy trạng thái từ TempData khi có lỗi
                }
                // ELSE: Nếu không có TempData["IsLoginState"] (ví dụ: lần đầu tải trang có lỗi),
                // IsLogin sẽ giữ giá trị mặc định là true (đăng nhập)
            }
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

            var loginRequest = new BusinessObject.DTOs.Login.LoginRequestDto { Email = Email, Password = Password };
            var content = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/login", content);

                // Đọc toàn bộ nội dung phản hồi dưới dạng chuỗi ngay lập tức
                var responseContentString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Xử lý trường hợp thành công
                    if (string.IsNullOrWhiteSpace(responseContentString))
                    {
                        ErrorMessage = "Server returned an empty response for successful login.";
                        _logger.LogWarning("API Login returned empty content for {Email} (success status).", Email);
                        TempData["IsLoginState"] = true;
                        return Page();
                    }

                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(
                            responseContentString,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (apiResponse?.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
                        {
                            SuccessMessage = apiResponse.Message;
                            _logger.LogInformation("User {Email} successfully logged in via API.", Email);

                            HttpContext.Response.Cookies.Append(
                                "AccessToken",
                                apiResponse.Data.Token,
                                new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(30) }
                            );
                            HttpContext.Response.Cookies.Append(
                                "RefreshToken",
                                apiResponse.Data.RefreshToken,
                                new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = apiResponse.Data.RefreshTokenExpiryTime }
                            );

                            return RedirectToPage("/Index");
                        }
                        else
                        {
                            ErrorMessage = apiResponse?.Message ?? "Login successful but no token received.";
                        }
                    }
                    catch (JsonException ex)
                    {
                        ErrorMessage = "Failed to parse successful login response from server. Invalid JSON format.";
                        _logger.LogError(ex, "JSON deserialization failed during successful login response for {Email}. Content: {ResponseContent}", Email, responseContentString);
                    }
                }
                else // response.IsSuccessStatusCode is false (có lỗi từ API backend)
                {
                    // Xử lý lỗi từ backend
                    if (!string.IsNullOrWhiteSpace(responseContentString))
                    {
                        try
                        {
                            // Luôn cố gắng deserialize phản hồi lỗi dưới dạng ApiResponse<string>
                            var apiError = JsonSerializer.Deserialize<ApiResponse<string>>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            ErrorMessage = apiError?.Message ?? $"API Login failed with status code: {response.StatusCode}.";
                        }
                        catch (JsonException ex)
                        {
                            // Nếu nội dung không phải JSON hợp lệ khi có lỗi, hoặc rỗng
                            ErrorMessage = $"API Login failed with status code: {response.StatusCode}. Server returned non-JSON or invalid JSON error: {responseContentString.Substring(0, Math.Min(responseContentString.Length, 200))}..."; // Chỉ lấy 200 ký tự đầu tiên
                            _logger.LogError(ex, "JSON deserialization failed for error response for {Email}. Status: {StatusCode}, Content: {ResponseContent}", Email, response.StatusCode, responseContentString);
                        }
                    }
                    else
                    {
                        // Nội dung phản hồi lỗi trống rỗng
                        ErrorMessage = $"API Login failed with status code: {response.StatusCode}. Server returned empty error response.";
                        _logger.LogWarning("API Login failed for {Email}: {StatusCode} - Empty error content", Email, response.StatusCode);
                    }
                    // Log chi tiết phản hồi lỗi để dễ debug hơn
                    _logger.LogWarning("API Login failed for {Email}: {StatusCode} - Full Error Content: {ErrorContent}", Email, response.StatusCode, responseContentString);
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to connect to the authentication server. Please try again later.";
                _logger.LogError(ex, "HTTP request failed during login for {Email}.", Email);
            }
            // Loại bỏ JsonException catch ở ngoài cùng, vì nó sẽ được bắt trong các try-catch nội bộ
            // catch (JsonException ex)
            // {
            //     ErrorMessage = "Failed to parse server response. Please try again.";
            //     _logger.LogError(ex, "JSON deserialization failed during login response for {Email}.", Email);
            // }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred during login.";
                _logger.LogError(ex, "Unhandled exception during login for {Email}.", Email);
            }

            TempData["IsLoginState"] = true;
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

            var registerRequest = new BusinessObject.DTOs.Login.RegisterRequest
            {
                Email = Email,
                Password = Password,
                FullName = Name,
            };
            var content = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/register", content);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<TokenResponseDto>>(
                        await response.Content.ReadAsStreamAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (apiResponse != null)
                    {
                        SuccessMessage = apiResponse.Message ?? "Registration successful! Please check your email to verify your account.";
                        _logger.LogInformation("User {Email} registered successfully via API.", Email);

                        TempData["SuccessMessage"] = SuccessMessage;
                        TempData["IsLoginState"] = true;
                        return RedirectToPage("/Auth");
                    }
                    else
                    {
                        ErrorMessage = "Registration successful but no response received.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var apiError = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = apiError?.Message ?? $"API Registration failed with status code: {response.StatusCode}.";
                    _logger.LogWarning("API Registration failed for {Email}: {StatusCode} - {ErrorContent}", Email, response.StatusCode, errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to connect to the authentication server. Please try again later.";
                _logger.LogError(ex, "HTTP request failed during registration for {Email}.", ex);
            }
            catch (JsonException ex)
            {
                ErrorMessage = "Failed to parse server response. Please try again.";
                _logger.LogError(ex, "JSON deserialization failed during registration response for {Email}.", ex);
            }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred during registration.";
                _logger.LogError(ex, "Unhandled exception during registration for {Email}.", ex);
            }

            TempData["IsLoginState"] = false;
            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostGoogleLogin([FromForm(Name = "IdToken")] string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                TempData["ErrorMessage"] = "Google ID Token is missing.";
                TempData["IsLoginState"] = true;
                return RedirectToPage("/Auth");
            }

            // Tạo DTO chỉ với IdToken
            var googleLoginRequest = new GoogleLoginRequestDto { IdToken = idToken };
            var content = new StringContent(JsonSerializer.Serialize(googleLoginRequest), Encoding.UTF8, "application/json");

            try
            {
                // Gọi API backend của bạn, chỉ truyền IdToken
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/google-login", content);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<TokenResponseDto>>(
                        await response.Content.ReadAsStreamAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (apiResponse?.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
                    {
                        TempData["SuccessMessage"] = apiResponse.Message ?? "Google login successful!";
                        _logger.LogInformation("Google user successfully logged in via API.");

                        // Lưu trữ token vào cookie
                        HttpContext.Response.Cookies.Append(
                            "AccessToken",
                            apiResponse.Data.Token,
                            new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(30) }
                        );
                        HttpContext.Response.Cookies.Append(
                            "RefreshToken",
                            apiResponse.Data.RefreshToken,
                            new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = apiResponse.Data.RefreshTokenExpiryTime }
                        );

                        return RedirectToPage("/Index");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Google login successful but no token received.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var apiError = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    TempData["ErrorMessage"] = apiError?.Message ?? $"Google API login failed with status code: {response.StatusCode}.";
                    _logger.LogWarning("Google API login failed: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                TempData["ErrorMessage"] = "Failed to connect to the authentication server for Google login. Please try again later.";
                _logger.LogError(ex, "HTTP request failed during Google login.");
            }
            catch (JsonException ex)
            {
                TempData["ErrorMessage"] = "Failed to parse Google login server response. Please try again.";
                _logger.LogError(ex, "JSON deserialization failed during Google login response.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred during Google login.";
                _logger.LogError(ex, "Unhandled exception during Google login.");
            }

            TempData["IsLoginState"] = true;
            return RedirectToPage("/Auth");
        }

        [ValidateAntiForgeryToken] // Ensure this is present if using Html.AntiForgeryToken() in the form
        public IActionResult OnPostLogout()
        {
            // Remove authentication cookies
            HttpContext.Response.Cookies.Delete("AccessToken");
            HttpContext.Response.Cookies.Delete("RefreshToken");

            // You might also clear session if you're using it for auth state
            // HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "You have been successfully logged out.";
            return RedirectToPage("/Auth"); // Redirect back to the login page
        }
    }
}