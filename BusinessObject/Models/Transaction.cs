using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.Models
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; }  // ID duy nhất cho mỗi giao dịch

        [Required]
        public Guid OrderId { get; set; }  // Liên kết đến đơn hàng tương ứng
        [ForeignKey("OrderId")]
        public Order Order { get; set; }   // Điều hướng tới đối tượng Order

        [Required]
        public Guid CustomerId { get; set; }  // ID khách hàng thực hiện giao dịch

        [Required]
        public Guid ProviderId { get; set; }  // ID nhà cung cấp liên quan đến giao dịch

        [Required, Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }  // Số tiền của giao dịch

        [Required]
        public TransactionStatus Status { get; set; }  // Trạng thái giao dịch (Ví dụ: Initiated, Completed, Failed)

        public string PaymentMethod { get; set; }  // Phương thức thanh toán (Ví dụ: VNPAY Card, QR code Bank,...)

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;  // Thời gian giao dịch được tạo

        public string? Content { get; set; } // Nội dung ghi chú từ thanh toán, ví dụ chứa OrderId
    }
}
