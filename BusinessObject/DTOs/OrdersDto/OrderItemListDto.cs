using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderItemListDto
    {
        public string ProductName { get; set; }
        public string ProductSize { get; set; }
        public string PrimaryImageUrl { get; set; }
        public int RentalDays { get; set; }
    }
}
