using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.TransactionsDto
{
    public class FinalizeCheckoutRequestDto
    {
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required][EmailAddress] public string Email { get; set; } = string.Empty;
        [Required][Phone] public string Phone { get; set; } = string.Empty;

        [Required] public string FullAddress { get; set; } = string.Empty;

        // Payment Method (Only VNPAY or QR)
        [Required] public string PaymentMethod { get; set; } = string.Empty;

        // Order Details inferred from Cart, but useful for backend verification
        public DateTime RentalStart { get; set; }
        public DateTime RentalEnd { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
