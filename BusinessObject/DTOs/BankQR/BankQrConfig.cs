using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.BankQR
{
    public class BankQrConfig
    {
        public string BankCode { get; set; }
        public string AccountNumber { get; set; }
        public string Template { get; set; }
    }
}
