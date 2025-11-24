using System;
using System.ComponentModel.DataAnnotations;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.IssueResolutionDto
{
    /// <summary>
    /// DTO để tạo quyết định cuối cùng từ Admin
    /// </summary>
    public class CreateIssueResolutionDto
    {
        /// <summary>
        /// ID của violation đang được giải quyết
        /// </summary>
        [Required(ErrorMessage = "Violation ID is required")]
        public Guid ViolationId { get; set; }

        /// <summary>
        /// Loại quyết định: UPHOLD_CLAIM, REJECT_CLAIM, COMPROMISE
        /// </summary>
        [Required(ErrorMessage = "Resolution type is required")]
        public ResolutionType ResolutionType { get; set; }

        /// <summary>
        /// Số tiền phạt cho customer (điều chỉnh nếu COMPROMISE)
        /// </summary>
        [Required(ErrorMessage = "Customer fine amount is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Customer fine amount must be non-negative")]
        public decimal CustomerFineAmount { get; set; }

        /// <summary>
        /// Số tiền bồi thường cho provider
        /// </summary>
        [Required(ErrorMessage = "Provider compensation amount is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Provider compensation amount must be non-negative")]
        public decimal ProviderCompensationAmount { get; set; }

        /// <summary>
        /// Ghi chú/lý do quyết định từ Admin (bắt buộc)
        /// </summary>
        [Required(ErrorMessage = "Admin notes/reason is required")]
        [StringLength(3000, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 3000 characters")]
        public string Reason { get; set; } = string.Empty;
    }
}

