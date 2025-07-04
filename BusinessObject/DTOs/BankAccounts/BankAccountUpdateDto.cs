using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.BankAccounts
{
    public class BankAccountUpdateDto
    {
        public Guid Id { get; set; }  // ID của tài khoản ngân hàng cần cập nhật
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? RoutingNumber { get; set; }
        public bool IsPrimary { get; set; }
    }
}
