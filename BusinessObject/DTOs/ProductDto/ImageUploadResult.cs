namespace BusinessObject.DTOs.ProductDto
{
    public class ImageUploadResult
    {
        // Thuộc tính này có thể bạn đã có
        public string ImageUrl { get; set; }

        // ✅ Đảm bảo bạn có dòng này
        public string PublicId { get; set; }
    }
}
