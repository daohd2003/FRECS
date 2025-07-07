using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderFullDetailsDto
    {
        public Guid Id { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? RentalStart { get; set; }
        public DateTime? RentalEnd { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Thông tin tóm tắt về Customer và Provider
        public UserDto Customer { get; set; }
        public UserDto Provider { get; set; }

        // Danh sách các sản phẩm trong đơn hàng
        public ICollection<OrderItemDto> Items { get; set; }

        // Danh sách các giao dịch liên quan đến đơn hàng
        public ICollection<TransactionSummaryDto> Transactions { get; set; }
    }
}
