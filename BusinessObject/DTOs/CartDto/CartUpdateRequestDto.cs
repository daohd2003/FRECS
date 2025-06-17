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
        [Required(ErrorMessage = "Rental Days (Quantity) is required for update.")]
        [Range(1, int.MaxValue, ErrorMessage = "Rental Days (Quantity) must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Rental Days is required for cart item update.")]
        [Range(1, int.MaxValue, ErrorMessage = "Rental Days must be at least 1.")]
        public int RentalDays { get; set; }
    }
}
