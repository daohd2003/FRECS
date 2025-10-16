using BusinessObject.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cho việc tạo 1 vi phạm cho 1 sản phẩm cụ thể
    /// KHÔNG CẦN cung cấp ViolationId - Hệ thống sẽ tự động generate khi tạo
    /// </summary>
    public class CreateRentalViolationDto
    {
        /// <summary>
        /// ID của OrderItem bị vi phạm
        /// </summary>
        [Required]
        public Guid OrderItemId { get; set; }

        /// <summary>
        /// Loại vi phạm
        /// </summary>
        [Required]
        [EnumDataType(typeof(ViolationType))]
        public ViolationType ViolationType { get; set; }

        /// <summary>
        /// Mô tả chi tiết về vi phạm
        /// </summary>
        [Required]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10-2000 ký tự")]
        public string Description { get; set; }

        /// <summary>
        /// Tỷ lệ % hư hỏng (chỉ cho ViolationType = DAMAGED)
        /// </summary>
        [Range(0, 100, ErrorMessage = "Tỷ lệ hư hỏng phải từ 0-100%")]
        public decimal? DamagePercentage { get; set; }

        /// <summary>
        /// Tỷ lệ % phạt (Provider nhập)
        /// </summary>
        [Required]
        [Range(0, 100, ErrorMessage = "Tỷ lệ phạt phải từ 0-100%")]
        public decimal PenaltyPercentage { get; set; }

        /// <summary>
        /// Số tiền phạt (tự động tính hoặc Provider nhập)
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Số tiền phạt không hợp lệ")]
        public decimal PenaltyAmount { get; set; }

        /// <summary>
        /// Danh sách file ảnh/video bằng chứng
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Cần ít nhất 1 ảnh/video bằng chứng")]
        public List<IFormFile> EvidenceFiles { get; set; }
    }
}


