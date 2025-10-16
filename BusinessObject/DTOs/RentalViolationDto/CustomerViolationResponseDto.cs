using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cho việc Customer phản hồi vi phạm (đồng ý hoặc từ chối)
    /// ViolationId được cung cấp trong URL path, KHÔNG PHẢI trong body
    /// </summary>
    public class CustomerViolationResponseDto
    {
        /// <summary>
        /// true = Đồng ý bồi thường, false = Từ chối yêu cầu
        /// </summary>
        [Required(ErrorMessage = "IsAccepted là bắt buộc")]
        public bool IsAccepted { get; set; }

        /// <summary>
        /// Ghi chú/lý do từ Customer (bắt buộc nếu IsAccepted = false)
        /// </summary>
        [StringLength(2000, ErrorMessage = "Ghi chú không được vượt quá 2000 ký tự")]
        public string? CustomerNotes { get; set; }

        /// <summary>
        /// Ảnh/video phản biện từ Customer (không bắt buộc)
        /// Thường được sử dụng khi Customer từ chối để cung cấp bằng chứng
        /// </summary>
        public List<IFormFile>? EvidenceFiles { get; set; }
    }
}


