using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObject.Models
{
    /// <summary>
    /// Lưu trữ ảnh AI Try-On của customer, tự động xóa sau 1 tuần
    /// </summary>
    public class TryOnImage
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public User Customer { get; set; }

        /// <summary>
        /// ID của sản phẩm được thử (nếu có)
        /// </summary>
        public Guid? ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        /// <summary>
        /// URL ảnh kết quả Try-On trên Cloudinary
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Public ID trên Cloudinary để xóa ảnh
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string CloudinaryPublicId { get; set; } = string.Empty;

        /// <summary>
        /// URL ảnh người dùng gốc (person image)
        /// </summary>
        [MaxLength(500)]
        public string? PersonImageUrl { get; set; }

        /// <summary>
        /// Public ID của ảnh person trên Cloudinary
        /// </summary>
        [MaxLength(200)]
        public string? PersonPublicId { get; set; }

        /// <summary>
        /// URL ảnh quần áo gốc (garment image)
        /// </summary>
        [MaxLength(500)]
        public string? GarmentImageUrl { get; set; }

        /// <summary>
        /// Public ID của ảnh garment trên Cloudinary
        /// </summary>
        [MaxLength(200)]
        public string? GarmentPublicId { get; set; }

        /// <summary>
        /// Loại trang phục (upper, lower, overall)
        /// </summary>
        [MaxLength(50)]
        public string? ClothingType { get; set; }

        /// <summary>
        /// Thời điểm tạo ảnh
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời điểm ảnh sẽ bị xóa (mặc định 1 tuần sau khi tạo)
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Đánh dấu ảnh đã bị xóa khỏi Cloudinary
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}
