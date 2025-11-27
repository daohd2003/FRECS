using BusinessObject.DTOs.TryOnDtos;

namespace Services.TryOnImageServices
{
    public interface ITryOnImageService
    {
        /// <summary>
        /// Lưu ảnh Try-On mới
        /// </summary>
        Task<TryOnImageDto> SaveTryOnImageAsync(Guid customerId, SaveTryOnImageRequest request);

        /// <summary>
        /// Lấy tất cả ảnh Try-On của customer
        /// </summary>
        Task<TryOnImageListResponse> GetCustomerTryOnImagesAsync(Guid customerId, int pageNumber = 1, int pageSize = 20);

        /// <summary>
        /// Lấy chi tiết một ảnh Try-On
        /// </summary>
        Task<TryOnImageDto?> GetTryOnImageByIdAsync(Guid imageId, Guid customerId);

        /// <summary>
        /// Xóa ảnh Try-On (xóa cả trên Cloudinary)
        /// </summary>
        Task<bool> DeleteTryOnImageAsync(Guid imageId, Guid customerId);

        /// <summary>
        /// Xử lý xóa các ảnh đã hết hạn (gọi bởi background service)
        /// </summary>
        Task<int> CleanupExpiredImagesAsync();
    }
}
