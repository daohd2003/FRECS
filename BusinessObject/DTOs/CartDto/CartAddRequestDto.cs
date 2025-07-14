using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CartDto
{
    public class CartAddRequestDto
    {
        [Required(ErrorMessage = "Product ID is required.")]
        public Guid ProductId { get; set; }

        [Required(ErrorMessage = "Quantity (RentalDays) is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity (RentalDays) must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Rental Days is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Rental Days must be at least 1.")]
        public int RentalDays { get; set; }

        [Required(ErrorMessage = "Start Date is required when adding to cart.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Size is required when adding to cart.")]
        public string Size { get; set; }
    }
}
