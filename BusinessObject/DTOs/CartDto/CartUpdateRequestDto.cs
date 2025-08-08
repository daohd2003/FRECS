using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CartDto
{
    public class CartUpdateRequestDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Rental Days (Quantity) must be at least 1.")]
        public int? Quantity { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Rental Days must be at least 1.")]
        public int? RentalDays { get; set; }

        // Optional: allow updating the rental start date from Cart
        public DateTime? StartDate { get; set; }
    }
}
