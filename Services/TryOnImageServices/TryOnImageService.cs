using BusinessObject.DTOs.TryOnDtos;
using BusinessObject.Models;
using Microsoft.Extensions.Logging;
using Repositories.TryOnImageRepositories;

namespace Services.TryOnImageServices
{
    public class TryOnImageService : ITryOnImageService
    {
        private readonly ITryOnImageRepository _repository;
        private readonly IAICloudinaryService _aiCloudinaryService;
        private readonly ILogger<TryOnImageService> _logger;

        // Thời gian hết hạn mặc định: 7 ngày
        private const int DefaultExpirationDays = 7;

        public TryOnImageService(
            ITryOnImageRepository repository,
            IAICloudinaryService aiCloudinaryService,
            ILogger<TryOnImageService> logger)
        {
            _repository = repository;
            _aiCloudinaryService = aiCloudinaryService;
            _logger = logger;
        }

        public async Task<TryOnImageDto> SaveTryOnImageAsync(Guid customerId, SaveTryOnImageRequest request)
        {
            // Chuyển đổi sang giờ Việt Nam (UTC+7)
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
            
            var tryOnImage = new TryOnImage
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductId = request.ProductId,
                ImageUrl = request.ImageUrl,
                CloudinaryPublicId = request.CloudinaryPublicId,
                PersonImageUrl = request.PersonImageUrl,
                PersonPublicId = request.PersonPublicId,
                GarmentImageUrl = request.GarmentImageUrl,
                GarmentPublicId = request.GarmentPublicId,
                ClothingType = request.ClothingType,
                CreatedAt = vietnamNow,
                ExpiresAt = vietnamNow.AddDays(DefaultExpirationDays),
                IsDeleted = false
            };

            await _repository.AddAsync(tryOnImage);

            _logger.LogInformation(
                "Saved Try-On image {ImageId} for customer {CustomerId}. Expires at {ExpiresAt}",
                tryOnImage.Id, customerId, tryOnImage.ExpiresAt);

            return MapToDto(tryOnImage);
        }

        public async Task<TryOnImageListResponse> GetCustomerTryOnImagesAsync(Guid customerId, int pageNumber = 1, int pageSize = 20)
        {
            var images = await _repository.GetByCustomerIdAsync(customerId, pageNumber, pageSize);
            var totalCount = await _repository.CountByCustomerIdAsync(customerId);

            return new TryOnImageListResponse
            {
                Images = images.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<TryOnImageDto?> GetTryOnImageByIdAsync(Guid imageId, Guid customerId)
        {
            var image = await _repository.GetByIdAsync(imageId);
            
            if (image == null || image.CustomerId != customerId || image.IsDeleted)
                return null;

            return MapToDto(image);
        }

        public async Task<bool> DeleteTryOnImageAsync(Guid imageId, Guid customerId)
        {
            var image = await _repository.GetByIdAsync(imageId);
            
            if (image == null || image.CustomerId != customerId || image.IsDeleted)
            {
                _logger.LogWarning("Try-On image {ImageId} not found or already deleted for customer {CustomerId}", imageId, customerId);
                return false;
            }

            _logger.LogInformation("Deleting Try-On image {ImageId}: Result={ResultId}, Person={PersonId}, Garment={GarmentId}", 
                imageId, image.CloudinaryPublicId, image.PersonPublicId ?? "null", image.GarmentPublicId ?? "null");

            // 1. Xóa ảnh trên Cloudinary (từng cái một, bắt lỗi riêng)
            try
            {
                // Xóa result image
                var resultDeleted = await _aiCloudinaryService.DeleteImageAsync(image.CloudinaryPublicId);
                _logger.LogInformation("Result image deletion: {Result}", resultDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting result image from Cloudinary");
            }

            try
            {
                // Xóa person image
                if (!string.IsNullOrEmpty(image.PersonPublicId))
                {
                    var personDeleted = await _aiCloudinaryService.DeleteImageAsync(image.PersonPublicId);
                    _logger.LogInformation("Person image deletion: {Result}", personDeleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting person image from Cloudinary");
            }

            try
            {
                // Xóa garment image
                if (!string.IsNullOrEmpty(image.GarmentPublicId))
                {
                    var garmentDeleted = await _aiCloudinaryService.DeleteImageAsync(image.GarmentPublicId);
                    _logger.LogInformation("Garment image deletion: {Result}", garmentDeleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting garment image from Cloudinary");
            }

            // 2. Xóa hoàn toàn record trong database (hard delete)
            try
            {
                await _repository.DeleteAsync(imageId);
                _logger.LogInformation("Successfully deleted Try-On image {ImageId} from database", imageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Try-On image {ImageId} from database", imageId);
                throw;
            }
        }

        public async Task<int> CleanupExpiredImagesAsync()
        {
            var expiredImages = await _repository.GetExpiredImagesAsync(100);
            var expiredList = expiredImages.ToList();

            if (!expiredList.Any())
            {
                _logger.LogDebug("No expired Try-On images to cleanup");
                return 0;
            }

            var deletedCount = 0;
            var deletedIds = new List<Guid>();

            foreach (var image in expiredList)
            {
                try
                {
                    // Xóa tất cả ảnh trên AI Cloudinary (result, person, garment)
                    await _aiCloudinaryService.DeleteImageAsync(image.CloudinaryPublicId);
                    
                    if (!string.IsNullOrEmpty(image.PersonPublicId))
                        await _aiCloudinaryService.DeleteImageAsync(image.PersonPublicId);
                    
                    if (!string.IsNullOrEmpty(image.GarmentPublicId))
                        await _aiCloudinaryService.DeleteImageAsync(image.GarmentPublicId);
                    
                    deletedIds.Add(image.Id);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting expired image {ImageId} from Cloudinary", image.Id);
                }
            }

            // Hard delete - xóa hoàn toàn khỏi database
            if (deletedIds.Any())
            {
                await _repository.HardDeleteBatchAsync(deletedIds);
            }

            _logger.LogInformation(
                "Cleanup completed: {DeletedCount}/{TotalCount} expired Try-On images hard deleted",
                deletedCount, expiredList.Count);

            return deletedCount;
        }

        private static TryOnImageDto MapToDto(TryOnImage image)
        {
            return new TryOnImageDto
            {
                Id = image.Id,
                CustomerId = image.CustomerId,
                ProductId = image.ProductId,
                ProductName = image.Product?.Name,
                ImageUrl = image.ImageUrl,
                PersonImageUrl = image.PersonImageUrl,
                GarmentImageUrl = image.GarmentImageUrl,
                ClothingType = image.ClothingType,
                CreatedAt = image.CreatedAt,
                ExpiresAt = image.ExpiresAt
            };
        }
    }
}
