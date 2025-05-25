using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.UserServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using BusinessObject.DTOs.CloudinarySetting;
using Microsoft.AspNetCore.Http;

namespace Services.CloudServices
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        private readonly CloudSettings _cloudSettings;

        private readonly IUserService _userService;

        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(IOptions<CloudSettings> cloudSettings, Cloudinary cloudinary, ILogger<CloudinaryService> logger, IUserService userService)
        {
            _cloudinary = cloudinary;
            _cloudSettings = cloudSettings.Value;
            _logger = logger;
            _userService = userService;
        }

        public async Task<string> UploadImage(IFormFile file, int userId, string projectName, string folderType)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty");
            }

            var user = await _userService.GetByIdAsync(userId);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                throw new ArgumentException("Only JPG/JPEG/PNG files are allowed");
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB
            {
                throw new ArgumentException("File size exceeds 5MB");
            }

            var publicId = $"{userId}";

            // 1. Kiểm tra và xóa ảnh cũ nếu tồn tại
            try
            {
                var deletionParams = new DeletionParams(publicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

                _logger.LogInformation($"Delete Old Image: {deletionResult.Result}");
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Not found"))
                {
                    throw new Exception($"Failed to delete old image: {ex.Message}");
                }
            }

            // 2. Upload ảnh mới
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                PublicId = publicId,
                Overwrite = true,
                Folder = $"{projectName}/{folderType}",
                Transformation = new Transformation()
            .Width(300).Height(300).Crop("fill") // Tự động resize
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Upload failed: {uploadResult.Error.Message}");
            }

            var imageUrl = uploadResult.SecureUrl.ToString();
            user.AvatarUrl = imageUrl;
            await _userService.UpdateAsync(user);

            return imageUrl;
        }
    }
}
