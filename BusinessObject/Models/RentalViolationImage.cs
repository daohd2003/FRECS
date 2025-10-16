using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    /// <summary>
    /// Bảng lưu trữ hình ảnh/video bằng chứng cho vi phạm
    /// Hỗ trợ upload từ cả Provider và Customer
    /// </summary>
    public class RentalViolationImage
    {
        /// <summary>
        /// Mã định danh duy nhất cho mỗi hình ảnh/video
        /// </summary>
        [Key]
        public Guid ImageId { get; set; }

        /// <summary>
        /// Liên kết đến biên bản vi phạm
        /// </summary>
        [Required]
        public Guid ViolationId { get; set; }

        [ForeignKey(nameof(ViolationId))]
        public RentalViolation Violation { get; set; }

        /// <summary>
        /// URL đầy đủ của hình ảnh/video từ Cloudinary
        /// Ví dụ: https://res.cloudinary.com/.../evidence_123.jpg
        /// </summary>
        [Required]
        [StringLength(500)]
        public string ImageUrl { get; set; }

        /// <summary>
        /// Xác định ai upload bằng chứng này: PROVIDER hoặc CUSTOMER
        /// Giúp phân biệt bằng chứng từ 2 bên khi hiển thị
        /// </summary>
        [Required]
        public EvidenceUploadedBy UploadedBy { get; set; }

        /// <summary>
        /// Loại file: 'image' hoặc 'video'
        /// Đồng nhất với Message.AttachmentType
        /// Giúp frontend render đúng component (img tag vs video tag)
        /// </summary>
        [StringLength(50)]
        public string? FileType { get; set; }

        /// <summary>
        /// Thời điểm upload
        /// </summary>
        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}

