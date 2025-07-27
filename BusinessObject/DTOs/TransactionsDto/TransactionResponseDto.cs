using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class TransactionResponseDto
    {
        public Guid TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string? QrImage { get; set; } // Base64 QR code image string
        public string? VnpayUrl { get; set; }
    }
}
