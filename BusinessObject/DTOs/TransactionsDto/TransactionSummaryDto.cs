using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class TransactionSummaryDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
        public string? Content { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime TransactionDate { get; set; }
        public List<OrderProviderPairDto> Orders { get; set; }
    }
}
