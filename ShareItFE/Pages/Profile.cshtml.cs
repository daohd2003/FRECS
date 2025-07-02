using BusinessObject.DTOs.ApiResponses;
using BusinessObject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        public ProfileModel(AuthenticatedHttpClientHelper clientHelper)
        {
            _clientHelper = clientHelper;
        }

        public bool IsPostBack { get; set; } = false;

        [BindProperty]
        public Profile Profile { get; set; }

        [BindProperty]
        public IFormFile UploadedAvatar { get; set; }

        // Thêm các thuộc tính cho dữ liệu khác
        public List<Order> Orders { get; set; } = new();
        public List<Product> Favorites { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var userId = Guid.Parse(userIdClaim.Value);

            // Lấy Profile
            var profileResponse = await client.GetAsync($"api/profile/{userId}");
            if (!profileResponse.IsSuccessStatusCode) return RedirectToPage("/Auth");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var profileApiResponse = JsonSerializer.Deserialize<ApiResponse<Profile>>(
                await profileResponse.Content.ReadAsStringAsync(), options);

            Profile = profileApiResponse?.Data;

            if (Profile == null) return NotFound("Profile not found.");

            // TODO: Lấy dữ liệu Orders và Favorites từ các API tương ứng
            // var ordersResponse = await client.GetAsync($"api/orders/user/{userId}");
            // Orders = JsonSerializer.Deserialize<ApiResponse<List<Order>>>(...).Data;

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var profileUpdateDto = new BusinessObject.DTOs.ProfileDtos.ProfileUpdateDto
            {
                FullName = Profile.FullName,
                Phone = Profile.Phone,
                Address = Profile.Address
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
        public async Task<IActionResult> OnPostUploadAvatarAsync()
        {
            if (UploadedAvatar == null || UploadedAvatar.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a file to upload.");
                return Page();
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();

            // Sử dụng MultipartFormDataContent để gửi file
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(UploadedAvatar.OpenReadStream());

            // Thêm file vào content. Tên "file" phải khớp với tham số IFormFile trong API Controller
            content.Add(streamContent, "file", UploadedAvatar.FileName);

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
    }
}