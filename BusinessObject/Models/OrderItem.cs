using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class OrderItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; }

        [Required]
        public Guid ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; }

        /// <summary>
        /// Số ngày thuê sản phẩm trong đơn hàng
        /// </summary>
        [Required]
        public int RentalDays { get; set; }

        /// <summary>
        /// Giá thuê mỗi ngày (lưu lại tại thời điểm đặt để tránh sai lệch nếu giá sản phẩm thay đổi)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal DailyRate { get; set; }
    }
}
