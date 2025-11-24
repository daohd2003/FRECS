using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    /// <summary>
    /// Bảng lưu trữ quyết định cuối cùng của Admin về tranh chấp vi phạm
    /// </summary>
    public class IssueResolution
    {
        /// <summary>
        /// Mã định danh duy nhất cho quyết định
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Liên kết đến RentalViolation - Mỗi violation chỉ có 1 resolution (One-to-One)
        /// </summary>
        [Required]
        public Guid ViolationId { get; set; }

        [ForeignKey(nameof(ViolationId))]
        public RentalViolation RentalViolation { get; set; }

        /// <summary>
        /// Số tiền phạt áp dụng cho Customer (có thể là 0 nếu admin reject claim)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal CustomerFineAmount { get; set; }

        /// <summary>
        /// Số tiền bồi thường cho Provider (có thể là 0 nếu admin reject claim)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ProviderCompensationAmount { get; set; }

        /// <summary>
        /// Loại quyết định: UPHOLD_CLAIM, REJECT_CLAIM, COMPROMISE
        /// </summary>
        [Required]
        public ResolutionType ResolutionType { get; set; }

        /// <summary>
        /// Lý do/Ghi chú của Admin khi đưa ra quyết định
        /// Bắt buộc phải có để giải thích rõ ràng cho cả hai bên
        /// </summary>
        [Required]
        [StringLength(3000)]
        public string Reason { get; set; }

        /// <summary>
        /// Trạng thái xử lý: PENDING, UNDER_REVIEW, COMPLETED
        /// </summary>
        [Required]
        public ResolutionStatus ResolutionStatus { get; set; } = ResolutionStatus.PENDING;

        /// <summary>
        /// Thời điểm Admin xử lý và đưa ra quyết định cuối cùng
        /// </summary>
        [Required]
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ID của Admin xử lý vụ việc
        /// </summary>
        [Required]
        public Guid ProcessedByAdminId { get; set; }

        [ForeignKey(nameof(ProcessedByAdminId))]
        public User ProcessedByAdmin { get; set; }
    }
}

