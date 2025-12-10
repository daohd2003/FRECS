using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO chi tiết đầy đủ cho RentalViolation (dùng cho trang xem chi tiết)
    /// </summary>
    public class RentalViolationDetailDto
    {
        public Guid ViolationId { get; set; }
        public Guid OrderItemId { get; set; }
        public ViolationType ViolationType { get; set; }
        public string ViolationTypeDisplay { get; set; } // "Sản phẩm bị hư hỏng", etc.
        public string Description { get; set; }
        public decimal? DamagePercentage { get; set; }
        public decimal PenaltyPercentage { get; set; }
        public decimal PenaltyAmount { get; set; }
        
        /// <summary>
        /// Tiền cọc gốc (tính từ OrderItem)
        /// </summary>
        public decimal DepositAmount { get; set; }
        
        /// <summary>
        /// Số tiền hoàn lại = DepositAmount - PenaltyAmount
        /// </summary>
        public decimal RefundAmount { get; set; }
        
        public ViolationStatus Status { get; set; }
        public string StatusDisplay { get; set; } // "Chờ phản hồi", "Đã đồng ý", etc.
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

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Thông tin sản phẩm bị vi phạm
        /// </summary>
        public OrderItemDetailsDto OrderItem { get; set; }

        /// <summary>
        /// Danh sách ảnh/video bằng chứng
        /// </summary>
        public List<RentalViolationImageDto> Images { get; set; }

        /// <summary>
        /// Ghi chú/lý do của Admin khi giải quyết tranh chấp
        /// Chỉ có giá trị khi Status = RESOLVED_BY_ADMIN
        /// </summary>
        public string? AdminResolutionNote { get; set; }

        /// <summary>
        /// Loại quyết định của Admin: UPHOLD_CLAIM, REJECT_CLAIM, COMPROMISE
        /// Chỉ có giá trị khi Status = RESOLVED_BY_ADMIN
        /// </summary>
        public string? AdminResolutionType { get; set; }
    }
}