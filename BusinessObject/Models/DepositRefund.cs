using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    /// <summary>
    /// Bảng riêng để track việc hoàn tiền cọc cho customer sau khi trả đồ
    /// TÁCH BIỆT với Transactions để KHÔNG ẢNH HƯỞNG đến logic thanh toán hiện tại
    /// </summary>
    public class DepositRefund
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Order được hoàn tiền cọc
        /// </summary>
        [Required]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; }

        /// <summary>
        /// Customer nhận tiền hoàn lại
        /// </summary>
        [Required]
        public Guid CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public User Customer { get; set; }

        /// <summary>
        /// Số tiền cọc ban đầu (từ Order.SecurityDeposit)
        /// </summary>
        [Required, Column(TypeName = "decimal(10,2)")]
        public decimal OriginalDepositAmount { get; set; }

        /// <summary>
        /// Tổng số tiền phạt (từ RentalViolations)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPenaltyAmount { get; set; } = 0;

        /// <summary>
        /// Số tiền thực tế hoàn lại = OriginalDepositAmount - TotalPenaltyAmount
        /// </summary>
        [Required, Column(TypeName = "decimal(10,2)")]
        public decimal RefundAmount { get; set; }

        /// <summary>
        /// Trạng thái hoàn tiền:
        /// - initiated: Đang chờ admin xử lý
        /// - completed: Đã hoàn thành
        /// - failed: Thất bại (lỗi chuyển khoản, etc.)
        /// </summary>
        [Required]
        public TransactionStatus Status { get; set; } = TransactionStatus.initiated;

        /// <summary>
        /// Bank Account được sử dụng để hoàn tiền (từ BankAccounts table của Customer)
        /// Admin sẽ chuyển tiền vào tài khoản này
        /// </summary>
        public Guid? RefundBankAccountId { get; set; }
        
        [ForeignKey(nameof(RefundBankAccountId))]
        public BankAccount? RefundBankAccount { get; set; }

        /// <summary>
        /// Ghi chú từ admin hoặc hệ thống
        /// </summary>
        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Admin nào xử lý hoàn tiền
        /// </summary>
        public Guid? ProcessedByAdminId { get; set; }

        [ForeignKey(nameof(ProcessedByAdminId))]
        public User? ProcessedByAdmin { get; set; }

        /// <summary>
        /// Thời gian tạo refund request (tự động khi customer trả đồ)
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời gian admin xử lý hoàn tiền
        /// </summary>
        public DateTime? ProcessedAt { get; set; }
    }
}

