using BusinessObject.DTOs.CloudinarySetting;
using BusinessObject.DTOs.ConversationDtos;
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

        // --- CATEGORY UPLOAD - Dedicated method for Category images ---
        public async Task<BusinessObject.DTOs.ProductDto.ImageUploadResult> UploadCategoryImageAsync(IFormFile file, Guid userId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file selected for category.");

            // Validate file type - only images allowed
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName)?.ToLower() ?? "";
            if (!allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"Invalid file type. Allowed: JPG, JPEG, PNG, GIF, WEBP. Got: {extension}");
            }

            // Validate file size (max 5MB for categories)
            const long maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
            {
                throw new ArgumentException($"File size exceeds 5MB limit. Size: {file.Length / 1024 / 1024}MB");
            }

            // Generate unique public ID for category image
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var randomPart = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var publicId = $"category_{userId}_{timestamp}_{randomPart}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                PublicId = publicId,
                Folder = $"ShareIt/categories/{userId}",  // Dedicated folder for categories
                Overwrite = false,
                Transformation = new Transformation()
                    .Width(1200)          // Max width
                    .Height(800)          // Max height
                    .Crop("limit")        // Maintain aspect ratio, only resize if larger
                    .Quality("auto:good") // Auto quality optimization
                    .FetchFormat("auto")  // Auto format selection (WebP if supported)
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            if (uploadResult.Error != null)
            {
                _logger.LogError($"Category image upload failed for user {userId}: {uploadResult.Error.Message}");
                throw new Exception($"Upload failed: {uploadResult.Error.Message}");
            }

            _logger.LogInformation($"Category image uploaded successfully. User: {userId}, PublicId: {uploadResult.PublicId}, URL: {uploadResult.SecureUrl}");

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

        public async Task<ChatAttachmentUploadResult> UploadChatAttachmentAsync(IFormFile file, Guid userId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file selected.");

            var extension = Path.GetExtension(file.FileName)?.ToLower() ?? string.Empty;
            var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension);
            var isVideo = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" }.Contains(extension);

            var folder = $"ShareIt/chat/{userId}";
            var publicId = $"chat_{userId}_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}";

            RawUploadParams uploadParams;
            if (isImage)
            {
                var imgParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, file.OpenReadStream()),
                    PublicId = publicId,
                    Folder = folder,
                    Overwrite = false,
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };
                var imgResult = await _cloudinary.UploadAsync(imgParams);
                if (imgResult.Error != null) throw new Exception(imgResult.Error.Message);
                return new ChatAttachmentUploadResult
                {
                    Url = imgResult.SecureUrl?.ToString(),
                    PublicId = imgResult.PublicId,
                    Type = "image",
                    ThumbnailUrl = imgResult.SecureUrl?.ToString(),
                    MimeType = file.ContentType,
                    FileName = file.FileName,
                    FileSize = file.Length
                };
            }
            else if (isVideo)
            {
                var vidParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, file.OpenReadStream()),
                    PublicId = publicId,
                    Folder = folder,
                    Overwrite = false,
                };
                var vidResult = await _cloudinary.UploadAsync(vidParams);
                if (vidResult.Error != null) throw new Exception(vidResult.Error.Message);

                // Optional: generate thumbnail URL using Cloudinary transformations
                var thumbnailUrl = _cloudinary.Api.UrlVideoUp.Secure(true)
                    .Transform(new Transformation().Width(320).Height(180).Crop("fill")).BuildUrl(vidResult.PublicId + ".jpg");

                return new ChatAttachmentUploadResult
                {
                    Url = vidResult.SecureUrl?.ToString(),
                    PublicId = vidResult.PublicId,
                    Type = "video",
                    ThumbnailUrl = thumbnailUrl,
                    MimeType = file.ContentType,
                    FileName = file.FileName,
                    FileSize = file.Length
                };
            }
            else
            {
                var rawParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, file.OpenReadStream()),
                    PublicId = publicId,
                    Folder = folder,
                    Overwrite = false,
                };
                var rawResult = await _cloudinary.UploadAsync(rawParams);
                if (rawResult.Error != null) throw new Exception(rawResult.Error.Message);
                return new ChatAttachmentUploadResult
                {
                    Url = rawResult.SecureUrl?.ToString(),
                    PublicId = rawResult.PublicId,
                    Type = "file",
                    ThumbnailUrl = null,
                    MimeType = file.ContentType,
                    FileName = file.FileName,
                    FileSize = file.Length
                };
            }
        }

        public async Task<BusinessObject.DTOs.ProductDto.ImageUploadResult> UploadMediaFileAsync(IFormFile file, Guid userId, string projectName, string folderType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file selected.");

            var extension = Path.GetExtension(file.FileName)?.ToLower() ?? string.Empty;
            var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(extension);
            var isVideo = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv", ".wmv" }.Contains(extension);

            if (!isImage && !isVideo)
            {
                throw new ArgumentException($"Unsupported file type: {extension}. Only images and videos are allowed.");
            }

            // Validate file size (10MB for images, 100MB for videos)
            var maxSize = isImage ? 10 * 1024 * 1024 : 100 * 1024 * 1024;
            if (file.Length > maxSize)
            {
                var maxSizeMB = isImage ? 10 : 100;
                throw new ArgumentException($"File size exceeds {maxSizeMB}MB limit. File size: {file.Length / 1024 / 1024}MB");
            }

            var publicId = $"{folderType}_{userId}_{Path.GetRandomFileName()}";
            var folder = $"{projectName}/{folderType}/{userId}";

            if (isImage)
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, file.OpenReadStream()),
                    PublicId = publicId,
                    Folder = folder,
                    Overwrite = false,
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    _logger.LogError($"Cloudinary image upload failed: {uploadResult.Error.Message}");
                    throw new Exception($"Upload failed: {uploadResult.Error.Message}");
                }

                return new BusinessObject.DTOs.ProductDto.ImageUploadResult
                {
                    ImageUrl = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId
                };
            }
            else // isVideo
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, file.OpenReadStream()),
                    PublicId = publicId,
                    Folder = folder,
                    Overwrite = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    _logger.LogError($"Cloudinary video upload failed: {uploadResult.Error.Message}");
                    throw new Exception($"Upload failed: {uploadResult.Error.Message}");
                }

                return new BusinessObject.DTOs.ProductDto.ImageUploadResult
                {
                    ImageUrl = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId
                };
            }
        }
    }
}
