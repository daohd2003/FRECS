using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CartDto
{
    public class CartDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
        public decimal TotalAmount { get; set; }
        
        /// <summary>
        /// Total deposit amount for all rental items in cart
        /// </summary>
        public decimal TotalDepositAmount { get; set; } = 0m;
    }
}
