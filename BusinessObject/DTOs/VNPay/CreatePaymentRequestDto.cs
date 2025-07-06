using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.VNPay
{
    public class CreatePaymentRequestDto
    {
        public List<Guid> OrderIds { get; set; } = new List<Guid>();
        public string Note { get; set; } = string.Empty;
    }
}
