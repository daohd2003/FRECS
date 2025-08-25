using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.NotificationDto;
using BusinessObject.DTOs.ProfileDtos;
using Microsoft.AspNetCore.Mvc;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShareItFE.Extensions;

namespace ShareItFE.ViewComponents.Header
{
    public class HeaderViewComponent : ViewComponent
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public HeaderViewComponent(IHttpContextAccessor httpContextAccessor, AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _httpContextAccessor = httpContextAccessor;
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }
        private string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new HeaderViewModel { IsUserLoggedIn = false };
            model.AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];
            var currentUser = _httpContextAccessor.HttpContext?.User;

            if (currentUser?.Identity?.IsAuthenticated == true)
            {
                model.IsUserLoggedIn = true;
                model.UserRole = currentUser.FindFirst(ClaimTypes.Role)?.Value;

                // Tạo HttpClient để gọi Backend API
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Lấy thông tin Header (Profile & Notifications)
                try
                {
                    // 1. Lấy thông tin profile
                    var profileResponse = await client.GetAsync($"api/profile/header-info");
                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var profileContent = await profileResponse.Content.ReadFromJsonAsync<UserHeaderInfoDto>();
                        model.UserName = profileContent?.FullName;
                        model.UserAvatarUrl = profileContent?.ProfilePictureUrl;
                    }

                    // 2. Lấy thông tin thông báo
                    var userIdClaim = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdClaim, out Guid userId))
                    {
                        model.UserId = userId; // Gán UserId vào model để dùng trong view

                        // Lấy số lượng thông báo chưa đọc
                        var unreadCountResponse = await client.GetAsync($"api/notification/unread-count/{userId}");
                        if (unreadCountResponse.IsSuccessStatusCode)
                        {
                            var apiResponse = await unreadCountResponse.Content.ReadFromJsonAsync<ApiResponse<int>>();
                            if (apiResponse != null)
                            {
                                model.UnreadNotificationCount = apiResponse.Data;
                            }
                        }

                        // 1. Tạo đối tượng options
                        var jsonOptions = new JsonSerializerOptions
                        {
                            // Cho phép chuyển đổi string thành enum
                            Converters = { new JsonStringEnumConverter() },
                            // Thêm tùy chọn này để bỏ qua sự khác biệt chữ hoa/thường (ví dụ: "type" và "Type")
                            PropertyNameCaseInsensitive = true
                        };

                        // Lấy danh sách thông báo
                        var notificationsResponse = await client.GetAsync($"api/notification/user/{userId}");
                        if (notificationsResponse.IsSuccessStatusCode)
                        {
                            var apiResponse = await notificationsResponse.Content.ReadFromJsonAsync<ApiResponse<List<NotificationResponse>>>(jsonOptions);
                            if (apiResponse != null)
                            {
                                model.Notifications = apiResponse.Data ?? new List<NotificationResponse>();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ghi lại lỗi chung cho việc lấy thông tin header
                    Console.WriteLine($"Error fetching header info (profile/notifications): {ex.Message}");
                }

                // Lấy thông tin giỏ hàng nếu là customer
                if (string.Equals(model.UserRole, "customer", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var cartResponse = await client.GetAsync("api/cart/count");
                        if (cartResponse.IsSuccessStatusCode)
                        {
                            var apiResponse = await cartResponse.Content.ReadFromJsonAsync<CartCountResponse>();
                            if (apiResponse != null)
                            {
                                model.CartItemCount = apiResponse.Count;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching cart count: {ex.Message}");
                    }
                }
            }

            return View(model);
        }
    }
}