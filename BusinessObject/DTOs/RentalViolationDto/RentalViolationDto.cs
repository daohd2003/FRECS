using BusinessObject.Enums;
using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cơ bản cho RentalViolation
    /// </summary>
    public class RentalViolationDto
    {
        public Guid ViolationId { get; set; }
        public Guid OrderItemId { get; set; }
        public ViolationType ViolationType { get; set; }
        public string Description { get; set; }
        public decimal? DamagePercentage { get; set; }
        public decimal PenaltyPercentage { get; set; }
        public decimal PenaltyAmount { get; set; }
        public ViolationStatus Status { get; set; }
        public string? CustomerNotes { get; set; }
        public DateTime? CustomerResponseAt { get; set; }

        /// <summary>
        /// Phản hồi của Provider đối với Customer's rejection
        /// </summary>
        public string? ProviderResponseToCustomer { get; set; }

        /// <summary>
        /// Thời điểm Provider phản hồi Customer's rejection
        /// </summary>
        public DateTime? ProviderResponseAt { get; set; }

        public string? ProviderEscalationReason { get; set; }
        public string? CustomerEscalationReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Số lượng ảnh/video bằng chứng
        /// </summary>
        public int EvidenceCount { get; set; }

        /// <summary>
        /// Danh sách URL của ảnh/video bằng chứng (để hiển thị khi edit)
        /// </summary>
        public List<string> EvidenceUrls { get; set; } = new List<string>();

        /// <summary>
        /// Danh sách chi tiết ảnh/video bằng chứng (bao gồm FileType để phân biệt image/video)
        /// </summary>
        public List<RentalViolationImageDto> EvidenceImages { get; set; } = new List<RentalViolationImageDto>();
    }
}