using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShareItFE.Extensions;

namespace ShareItFE.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ProfileModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public bool IsPostBack { get; set; } = false;

        [BindProperty]
        public Profile Profile { get; set; }

        // Thêm các thuộc tính cho dữ liệu khác
        public List<OrderListDto> Orders { get; set; } = new List<OrderListDto>();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalPages => (int)Math.Ceiling((double)(Orders?.Count ?? 0) / PageSize);

        public List<Favorite> Favorites { get; set; } = new();
        public int FavoritesPageNum { get; set; } = 1;
        public int FavoritesPageSize { get; set; } = 6;
        public int FavoritesTotalPages => (int)Math.Ceiling((double)(Favorites?.Count ?? 0) / FavoritesPageSize);

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [BindProperty]
        public ChangePasswordRequest ChangePassword { get; set; }

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);
        public string AccessToken { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNum = 1, int favPage = 1, string tab = "profile")
        {
            CurrentPage = pageNum;
            FavoritesPageNum = favPage;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            AccessToken = Request.Cookies["AccessToken"];

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var userId = Guid.Parse(userIdClaim.Value);

            // Chuẩn bị các tùy chọn deserialize, sử dụng lại cho tất cả các request
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // --- 1. Lấy Profile ---
            var profileResponse = await client.GetAsync($"api/profile/{userId}");
            if (!profileResponse.IsSuccessStatusCode) return RedirectToPage("/Auth");

            var profileApiResponse = JsonSerializer.Deserialize<ApiResponse<Profile>>(
                await profileResponse.Content.ReadAsStringAsync(), options);

            Profile = profileApiResponse?.Data;
            if (Profile == null) return NotFound("Profile not found.");

            // --- 2. Lấy Orders ---
            var ordersResponse = await client.GetAsync($"api/orders/customer/{userId}/list-display");
            if (ordersResponse.IsSuccessStatusCode)
            {
                var ordersContent = await ordersResponse.Content.ReadAsStringAsync();
                Console.WriteLine(ordersContent);
                var ordersApiResponse = JsonSerializer.Deserialize<ApiResponse<List<OrderListDto>>>(ordersContent, options);

                // Gán dữ liệu hoặc một list DTO rỗng nếu data là null
                Orders = ordersApiResponse?.Data ?? new List<OrderListDto>();
            }

            // --- 3. Lấy Favorites ---
            // Giả sử API endpoint cho favorites là 'api/favorites/{userId}'
            var favoritesResponse = await client.GetAsync($"api/favorites/{userId}");
            if (favoritesResponse.IsSuccessStatusCode)
            {
                var favoritesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<Favorite>>>(
                    await favoritesResponse.Content.ReadAsStringAsync(), options);
                Favorites = favoritesApiResponse?.Data ?? new List<Favorite>();
            }

            /*
             * Ghi chú về tối ưu hóa: 
             * Các lệnh gọi API trên đang được thực thi tuần tự. 
             * Để tăng tốc độ tải trang, bạn có thể chạy chúng song song bằng Task.WhenAll.
             * Ví dụ: await Task.WhenAll(profileTask, ordersTask, favoritesTask);
            */

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var profileUpdateDto = new BusinessObject.DTOs.ProfileDtos.ProfileUpdateDto
            {
                FullName = Profile.FullName,
                Phone = Profile.Phone ?? string.Empty,
                Address = Profile.Address ?? string.Empty
            };

            var client = await _clientHelper.GetAuthenticatedClientAsync();

            var response = await client.PutAsJsonAsync($"api/profile/{Profile.UserId}", profileUpdateDto);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Failed to update profile: {errorContent}");

                await OnGetAsync();
                return Page();
            }

            return RedirectToPage();
        }

        // Thêm Page Handler mới để xử lý việc upload
        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile uploadedAvatar)
        {
            if (uploadedAvatar == null || uploadedAvatar.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a file to upload.");
                return Page();
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();

            // Sử dụng MultipartFormDataContent để gửi file
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(uploadedAvatar.OpenReadStream());

            // Thêm file vào content. Tên "file" phải khớp với tham số IFormFile trong API Controller
            content.Add(streamContent, "file", uploadedAvatar.FileName);

            // Gọi đến API endpoint upload-image
            var response = await client.PostAsync("api/profile/upload-image", content);

            if (response.IsSuccessStatusCode)
            {
                // Giả sử API trả về JSON chứa URL của ảnh
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(
                    await response.Content.ReadAsStringAsync(), options);

                var newImageUrl = apiResponse?.Data;

                // TODO: Sau khi có URL ảnh mới, bạn cần gọi một action khác để
                // lưu URL này vào Profile của user trong database.
                // Ví dụ: await _profileService.UpdateAvatarUrl(userId, newImageUrl);

                TempData["SuccessMessage"] = "Avatar updated successfully!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Upload failed: {error}");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveFavoriteAsync(Guid productId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            var userId = Guid.Parse(userIdClaim.Value);

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var response = await client.DeleteAsync($"api/favorites?userId={userId}&productId={productId}");

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Removed from favorites successfully.";
            }
            else
            {
                SuccessMessage = "Failed to remove favorite.";
            }

            return RedirectToPage(new { tab = "favorites", pageNum = CurrentPage, favPage = FavoritesPageNum });
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                ErrorMessage = string.Join(" ", errors);
                return RedirectToPage(new { tab = "settings" });
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();

            // Tạo request body khớp với yêu cầu của API
            var changePasswordRequest = new ChangePasswordRequest
            {
                CurrentPassword = ChangePassword.CurrentPassword,
                NewPassword = ChangePassword.NewPassword,
                ConfirmPassword = ChangePassword.ConfirmPassword
            };

            var response = await client.PostAsJsonAsync("api/auth/change-password", changePasswordRequest);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Password changed successfully!";
            }
            else
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                ErrorMessage = errorResponse?.Message ?? "An error occurred while changing the password.";
            }

            return RedirectToPage(new { tab = "settings" });
        }
    }
}