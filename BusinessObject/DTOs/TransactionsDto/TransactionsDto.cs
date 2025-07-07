using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class TransactionDetailsDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Content { get; set; }

        // Thông tin tóm tắt về các đơn hàng thuộc giao dịch này
        public ICollection<OrderSummaryDto> Orders { get; set; }
    }
}
