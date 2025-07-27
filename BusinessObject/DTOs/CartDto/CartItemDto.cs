using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CartDto
{
    public class CartItemDto
    {
        public Guid ItemId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductSize { get; set; }
        public int Quantity { get; set; }
        public int RentalDays { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal TotalItemPrice { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PrimaryImageUrl { get; set; }
    }
}
