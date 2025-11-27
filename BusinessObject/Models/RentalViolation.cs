using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    /// <summary>
    /// Bảng lưu trữ thông tin về vi phạm liên quan đến đơn hàng cho thuê
    /// </summary>
    public class RentalViolation
    {
        /// <summary>
        /// Mã định danh duy nhất cho mỗi vi phạm
        /// </summary>
        [Key]
        public Guid ViolationId { get; set; }

        /// <summary>
        /// Liên kết đến OrderItem cụ thể bị vi phạm
        /// Quan trọng: Liên kết trực tiếp đến OrderItem, không phải Order
        /// </summary>
        [Required]
        public Guid OrderItemId { get; set; }

        [ForeignKey(nameof(OrderItemId))]
        public OrderItem OrderItem { get; set; }

        /// <summary>
        /// Loại vi phạm: DAMAGED, LATE_RETURN, NOT_RETURNED
        /// </summary>
        [Required]
        public ViolationType ViolationType { get; set; }

        /// <summary>
        /// Mô tả chi tiết về vi phạm từ Provider
        /// Ví dụ: "Màn hình bị nứt góc trên bên trái, vết nứt dài 5cm"
        /// </summary>
        [Required]
        [StringLength(2000)]
        public string Description { get; set; }

        /// <summary>
        /// Tỷ lệ phần trăm hư hỏng của sản phẩm (0.00 - 100.00)
        /// Ví dụ: 30.00 = 30% hư hỏng
        /// Chỉ áp dụng khi ViolationType = DAMAGED
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? DamagePercentage { get; set; }

        /// <summary>
        /// Tỷ lệ phần trăm phạt hiện tại (0.00 - 100.00)
        /// Ví dụ: 50.00 = phạt 50% tiền cọc
        /// Có thể thay đổi khi Provider điều chỉnh
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PenaltyPercentage { get; set; }

        /// <summary>
        /// Số tiền phạt/bồi thường hiện tại
        /// Có thể thay đổi khi Provider điều chỉnh sau khi Customer từ chối
        /// Công thức ban đầu: (OrderItem.DepositPerUnit × Quantity) × (PenaltyPercentage / 100)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal PenaltyAmount { get; set; }

        /// <summary>
        /// Trạng thái của biên bản vi phạm
        /// PENDING → CUSTOMER_ACCEPTED/REJECTED → RESOLVED
        /// </summary>
        [Required]
        public ViolationStatus Status { get; set; } = ViolationStatus.PENDING;

        /// <summary>
        /// Ghi chú/lý do từ Customer khi từ chối
        /// Ví dụ: "Sản phẩm đã hỏng từ trước khi giao, tôi có video mở hàng"
        /// Reset về null khi Provider gửi lại yêu cầu mới
        /// </summary>
        [StringLength(2000)]
        public string? CustomerNotes { get; set; }

        /// <summary>
        /// Thời điểm Customer đưa ra phản hồi (đồng ý/từ chối)
        /// Reset về null khi Provider gửi lại yêu cầu mới
        /// </summary>
        public DateTime? CustomerResponseAt { get; set; }

        /// <summary>
        /// Phản hồi của Provider đối với Customer's rejection notes
        /// Provider có thể giải thích hoặc phản bác lý do từ chối của Customer
        /// </summary>
        [StringLength(2000)]
        public string? ProviderResponseToCustomer { get; set; }

        /// <summary>
        /// Thời điểm Provider phản hồi Customer's rejection
        /// </summary>
        public DateTime? ProviderResponseAt { get; set; }

        /// <summary>
        /// Lý do Provider escalate lên Admin
        /// Chỉ có giá trị khi Provider escalate dispute
        /// </summary>
        [StringLength(2000)]
        public string? ProviderEscalationReason { get; set; }

        /// <summary>
        /// Lý do Customer escalate lên Admin
        /// Chỉ có giá trị khi Customer escalate dispute
        /// </summary>
        [StringLength(2000)]
        public string? CustomerEscalationReason { get; set; }

        /// <summary>
        /// Ngày/giờ tạo biên bản vi phạm
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Ngày/giờ cập nhật cuối cùng
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Danh sách hình ảnh/video bằng chứng từ cả Provider và Customer
        /// </summary>
        public ICollection<RentalViolationImage> Images { get; set; } = new List<RentalViolationImage>();
    }
}

