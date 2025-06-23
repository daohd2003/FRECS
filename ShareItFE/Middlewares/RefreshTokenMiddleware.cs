using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // Đảm bảo có
using System.Net.Http; // Đảm bảo có

namespace ShareItFE.Middlewares
{
    public class RefreshTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RefreshTokenMiddleware> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public RefreshTokenMiddleware(RequestDelegate next, ILogger<RefreshTokenMiddleware> logger,
                                      IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // --- 1. Lọc các yêu cầu không cần xử lý ---
            // Bỏ qua các đường dẫn tĩnh, đường dẫn liên quan đến xác thực/đăng ký
            // và các đường dẫn nội bộ của Razor Pages.
            // Điều này đảm bảo middleware không chạy không cần thiết cho mọi resource.
            if (context.Request.Path.StartsWithSegments("/Auth") ||
                context.Request.Path.StartsWithSegments("/_") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/lib") ||
                context.Request.Path.StartsWithSegments("/favicon.ico"))
            {
                await _next(context); // Chuyển tiếp yêu cầu mà không xử lý
                return;
            }

            // --- 2. Lấy token từ cookie ---
            // AccessToken sẽ là null/rỗng nếu cookie đã hết hạn trên trình duyệt.
            var accessToken = context.Request.Cookies["AccessToken"];
            var refreshToken = context.Request.Cookies["RefreshToken"];

            // --- 3. Kịch bản cần làm mới token: AccessToken bị thiếu nhưng RefreshToken còn ---
            // Chúng ta chỉ cố gắng làm mới khi AccessToken không có, nhưng RefreshToken vẫn tồn tại.
            if (string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogInformation("AccessToken is missing from cookies, but RefreshToken is present. Attempting to refresh token.");

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7256/api";

                try
                {
                    using var client = _httpClientFactory.CreateClient(); // Sử dụng HttpClientFactory
                    var refreshRequest = new RefreshTokenRequestDto { RefreshToken = refreshToken };
                    var content = new StringContent(JsonSerializer.Serialize(refreshRequest), Encoding.UTF8, "application/json");

                    // Gọi API Refresh Token ở Backend (nơi xử lý xác thực RefreshToken đầy đủ)
                    var apiResponse = await client.PostAsync($"{apiBaseUrl}/auth/refresh-token", content);
                    var responseContent = await apiResponse.Content.ReadAsStringAsync();

                    if (apiResponse.IsSuccessStatusCode)
                    {
                        var tokenResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponseDto>>(
                            responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (tokenResponse?.Data != null && !string.IsNullOrEmpty(tokenResponse.Data.Token))
                        {
                            _logger.LogInformation("Token refreshed successfully by API. Updating cookies.");

                            // Cập nhật AccessToken và RefreshToken trong cookie với giá trị mới
                            context.Response.Cookies.Append(
                                "AccessToken",
                                tokenResponse.Data.Token,
                                new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(30) }
                            );
                            context.Response.Cookies.Append(
                                "RefreshToken",
                                tokenResponse.Data.RefreshToken,
                                new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = tokenResponse.Data.RefreshTokenExpiryTime }
                            );
                            // Sau khi cookie được cập nhật, request sẽ tiếp tục xử lý
                            // và Authentication middleware (nếu có) sẽ nhận diện được token mới.
                        }
                        else
                        {
                            // API trả về thành công nhưng không có token mới hợp lệ (trạng thái không mong muốn)
                            _logger.LogWarning("API refresh call succeeded but no valid tokens were returned. Forcing re-login.");
                            ForceReLogin(context, "session_expired_no_new_token");
                            return; // Ngăn request tiếp tục
                        }
                    }
                    else // API Refresh Token trả về lỗi (401, 400, v.v. - RefreshToken không hợp lệ/hết hạn)
                    {
                        _logger.LogWarning("API Refresh Token failed. Status: {StatusCode}, Response: {ResponseContent}. Forcing re-login.", apiResponse.StatusCode, responseContent);
                        ForceReLogin(context, "session_expired_refresh_failed");
                        return; // Ngăn request tiếp tục
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error during token refresh call to API Backend. Forcing re-login.");
                    ForceReLogin(context, "network_error");
                    return;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization error from API Refresh Token response. Forcing re-login.");
                    ForceReLogin(context, "invalid_api_response");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred during token refresh. Forcing re-login.");
                    ForceReLogin(context, "unexpected_error");
                    return;
                }
            }
            // --- 4. Chuyển tiếp yêu cầu ---
            // Nếu không thuộc kịch bản cần làm mới (ví dụ: đã có AccessToken, hoặc không có RefreshToken),
            // hoặc đã làm mới thành công, chuyển tiếp yêu cầu đến middleware tiếp theo.
            await _next(context);
        }

        // --- Hàm hỗ trợ xóa cookie và chuyển hướng đăng nhập ---
        private void ForceReLogin(HttpContext context, string errorReason)
        {
            ClearAuthCookies(context);
            // Chỉ redirect nếu phản hồi chưa được gửi đi.
            if (!context.Response.HasStarted)
            {
                context.Response.Redirect($"/Auth?error={errorReason}");
            }
            else
            {
                _logger.LogError("Cannot redirect to login page as response has already started for error: {ErrorReason}", errorReason);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized; // Hoặc 403 Forbidden
            }
        }

        private void ClearAuthCookies(HttpContext context)
        {
            context.Response.Cookies.Delete("AccessToken", new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax });
            context.Response.Cookies.Delete("RefreshToken", new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax });
            _logger.LogInformation("Authentication cookies cleared by RefreshTokenMiddleware.");
        }
    }
}