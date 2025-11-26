namespace Services.TryOnImageServices
{
    /// <summary>
    /// Service để quản lý ảnh trên Cloudinary của AI Try-On service
    /// </summary>
    public interface IAICloudinaryService
    {
        /// <summary>
        /// Xóa ảnh trên AI Cloudinary
        /// </summary>
        Task<bool> DeleteImageAsync(string publicId);
    }
}
