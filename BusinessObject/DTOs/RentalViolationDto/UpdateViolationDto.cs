using System.ComponentModel.DataAnnotations;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cho việc Provider điều chỉnh lại yêu cầu (sau khi Customer từ chối)
    /// </summary>
    public class UpdateViolationDto
    {
        /// <summary>
        /// Loại vi phạm mới (optional)
        /// </summary>
        public ViolationType? ViolationType { get; set; }

        /// <summary>
        /// Mô tả mới (optional - có thể giữ nguyên)
        /// </summary>
        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Tỷ lệ % hư hại mới (optional)
        /// </summary>
        [Range(0, 100, ErrorMessage = "Tỷ lệ hư hại phải từ 0-100%")]
        public decimal? DamagePercentage { get; set; }

        /// <summary>
        /// Tỷ lệ % phạt mới
        /// </summary>
        [Range(0, 100, ErrorMessage = "Tỷ lệ phạt phải từ 0-100%")]
        public decimal? PenaltyPercentage { get; set; }

        /// <summary>
        /// Số tiền phạt mới
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "Số tiền phạt không hợp lệ")]
        public decimal? PenaltyAmount { get; set; }
    }
}