using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderItemDto
    {
        public Guid ProductId { get; set; }
        public int RentalDays { get; set; }
        public decimal DailyRate { get; set; }
    }
}
