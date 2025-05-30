using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class CreateTransactionRequest
    {
        public Guid OrderId { get; set; }
        public Guid ProviderId { get; set; }
        public decimal Amount { get; set; }
        public string? Content { get; set; }
    }
}
