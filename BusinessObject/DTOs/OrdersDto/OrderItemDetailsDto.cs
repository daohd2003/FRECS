using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderItemDetailsDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }     // Từ Product.Name
        public string? Size { get; set; }            // Từ Product.Size
        public string? Color { get; set; }           // Từ Product.Color
        public string PrimaryImageUrl { get; set; } // Từ Product.Images
        public int Quantity { get; set; }            // Từ OrderItem.Quantity
        public int RentalDays { get; set; }          // Từ OrderItem.RentalDays
        public decimal PricePerDay { get; set; }     // Từ OrderItem.DailyRate
    }
}
