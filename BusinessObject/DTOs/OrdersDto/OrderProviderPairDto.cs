using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderProviderPairDto
    {
        public Guid OrderId { get; set; }
        public Guid ProviderId { get; set; }
        public decimal OrderAmount { get; set; }
    }
}
