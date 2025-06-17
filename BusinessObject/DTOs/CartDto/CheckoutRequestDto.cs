using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CartDto
{
    public class CheckoutRequestDto
    {
        [Required(ErrorMessage = "Rental start date is required.")]
        public DateTime RentalStart { get; set; }

        [Required(ErrorMessage = "Rental end date is required.")]
        public DateTime RentalEnd { get; set; }
    }
}
