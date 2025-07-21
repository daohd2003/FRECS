using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class RentAgainRequestDto
    {
        public Guid OriginalOrderId { get; set; }
        public DateTime NewRentalStartDate { get; set; }
        public DateTime NewRentalEndDate { get; set; }
    }
}
