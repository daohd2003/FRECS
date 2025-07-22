using BusinessObject.DTOs.CloudinarySetting;
using BusinessObject.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.ProfileServices;
using Services.UserServices;

namespace Services.CloudServices
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        private readonly CloudSettings _cloudSettings;

        private readonly IUserService _userService;

        private readonly IProfileService _profileService;

        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(IOptions<CloudSettings> cloudSettings, Cloudinary cloudinary, ILogger<CloudinaryService> logger, IUserService userService, IProfileService profileService)
        {
            _cloudinary = cloudinary;
            _cloudSettings = cloudSettings.Value;
            _logger = logger;
            _userService = userService;
            _profileService = profileService;
        }

        public async Task<string> UploadImage(IFormFile file, Guid userId, string projectName, string folderType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            // Lấy User cùng Profile luôn, nếu bạn có navigation property
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // Giả sử bạn có 1 service ProfileService hoặc tự query DbContext
            var profile = await _profileService.GetByUserIdAsync(userId);
            if (profile == null)
            {
                profile = new Profile
                {
                    UserId = user.Id,
                    FullName = "", // hoặc lấy từ user
                    Phone = "",
                    Address = "",
                    ProfilePictureUrl = ""
                };
                await _profileService.AddAsync(profile);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName)?.ToLower() ?? "";
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("Only JPG/JPEG/PNG files are allowed");

            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("File size exceeds 5MB");

            var publicId = $"{userId}";

            try
            {
                var deletionParams = new DeletionParams(publicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);
                _logger.LogInformation($"Deleted old image result: {deletionResult.Result}");
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Not found"))
                    throw new Exception($"Failed to delete old image: {ex.Message}");
            }

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                PublicId = publicId,
                Overwrite = true,
                Folder = $"{projectName}/{folderType}",
                Transformation = new Transformation().Width(300).Height(300).Crop("fill")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                throw new Exception($"Upload failed: {uploadResult.Error.Message}");

            var imageUrl = uploadResult.SecureUrl.ToString();

            // Lưu ảnh vào Profile
            profile.ProfilePictureUrl = imageUrl;
            await _profileService.UpdateAsync(profile);

            return imageUrl;
        }
        public async Task<BusinessObject.DTOs.ProductDto.ImageUploadResult> UploadSingleImageAsync(IFormFile file, Guid userId, string projectName, string folderType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file selected.");

            // Validation (có thể thêm kiểm tra size, loại file...)

            var publicId = $"product_{userId}_{Path.GetRandomFileName()}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                PublicId = publicId,
                Folder = $"{projectName}/{folderType}/{userId}",
                Overwrite = false,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
            {
                _logger.LogError($"Cloudinary upload failed: {uploadResult.Error.Message}");
                throw new Exception($"Upload failed: {uploadResult.Error.Message}");
            }

            return new BusinessObject.DTOs.ProductDto.ImageUploadResult
            {
                ImageUrl = uploadResult.SecureUrl.ToString(),
                PublicId = uploadResult.PublicId
            };
        }

        // --- PHƯƠNG THỨC UPLOAD NHIỀU ẢNH ---
        public async Task<List<BusinessObject.DTOs.ProductDto.ImageUploadResult>> UploadMultipleImagesAsync(IFormFileCollection files, Guid userId, string projectName, string folderType)
        {
            if (files == null || files.Count == 0)
                throw new ArgumentException("No files selected.");

            var uploadResults = new List<BusinessObject.DTOs.ProductDto.ImageUploadResult>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                // Có thể gọi lại hàm upload 1 ảnh để tránh lặp code
                var result = await UploadSingleImageAsync(file, userId, projectName, folderType);
                uploadResults.Add(result);
            }

            return uploadResults;
        }
        public async Task<bool> DeleteImageAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId)) return false;

            var deletionParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deletionParams);

            // "ok" nghĩa là xóa thành công, "not found" cũng có thể coi là thành công
            return result.Result.ToLower() == "ok" || result.Result.ToLower() == "not found";
        }
    }
}
