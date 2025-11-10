using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.CartDto
{
    public class CartAddRequestDto
    {
        [Required(ErrorMessage = "Product ID is required.")]
        public Guid ProductId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        /// <summary>
        /// Loại giao dịch: thuê (Rental) hoặc mua (Purchase)
        /// </summary>
        public TransactionType TransactionType { get; set; } = TransactionType.rental;

        /// <summary>
        /// Số ngày thuê (chỉ áp dụng cho Rental)
        /// Giới hạn tối đa 30 ngày
        /// </summary>
        [Range(1, 30, ErrorMessage = "Rental Days must be between 1 and 30.")]
        public int? RentalDays { get; set; }

        /// <summary>
        /// Ngày bắt đầu thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "Size is required when adding to cart.")]
        public string Size { get; set; }
    }
}
