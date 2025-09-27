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
    /// <summary>
    /// Mục sản phẩm trong giỏ hàng
    /// </summary>
    public class CartItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CartId { get; set; }

        [ForeignKey(nameof(CartId))]
        public Cart Cart { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        /// <summary>
        /// Loại giao dịch: thuê (Rental) hoặc mua (Purchase)
        /// </summary>
        [Required]
        public TransactionType TransactionType { get; set; } = TransactionType.rental;

        /// <summary>
        /// Số ngày thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public int? RentalDays { get; set; }

        /// <summary>
        /// Ngày bắt đầu thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Ngày kết thúc thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public DateTime? EndDate { get; set; }

    }
}
