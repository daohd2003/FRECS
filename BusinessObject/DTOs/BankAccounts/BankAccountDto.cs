using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.BankAccounts
{
    public class BankAccountDto
    {
        public Guid Id { get; set; }  // Dùng cho Update
        public Guid ProviderId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? RoutingNumber { get; set; }
        public bool IsPrimary { get; set; }
    }
}
