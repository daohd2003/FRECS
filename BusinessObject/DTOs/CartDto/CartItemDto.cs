using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.CartDto
{
    public class CartItemDto
    {
        public Guid ItemId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductSize { get; set; }
        public int Quantity { get; set; }
        
        /// <summary>
        /// Loại giao dịch: thuê (Rental) hoặc mua (Purchase)
        /// </summary>
        public TransactionType TransactionType { get; set; } = TransactionType.rental;
        
        /// <summary>
        /// Số ngày thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public int? RentalDays { get; set; }
        
        public decimal PricePerUnit { get; set; }
        public decimal TotalItemPrice { get; set; }

        /// <summary>
        /// Ngày bắt đầu thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public DateTime? StartDate { get; set; }
        
        /// <summary>
        /// Ngày kết thúc thuê (chỉ áp dụng cho Rental)
        /// </summary>
        public DateTime? EndDate { get; set; }
        
        public string PrimaryImageUrl { get; set; }
    }
}
