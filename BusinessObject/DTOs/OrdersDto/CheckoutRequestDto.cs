using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class  CheckoutRequestDto
    {
        [Required(ErrorMessage = "Rental start date is required.")]
        public DateTime RentalStart { get; set; }

        [Required(ErrorMessage = "Rental end date is required.")]
        public DateTime RentalEnd { get; set; }

        public string? CustomerFullName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhoneNumber { get; set; }
        public string? DeliveryAddress { get; set; }
        public bool UseSameProfile { get; set; } = true;
    }
}
