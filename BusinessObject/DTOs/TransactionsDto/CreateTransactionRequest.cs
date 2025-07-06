using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class CreateTransactionRequest
    {
        public List<Guid> OrderIds { get; set; }
        public string? Content { get; set; }
    }
}
