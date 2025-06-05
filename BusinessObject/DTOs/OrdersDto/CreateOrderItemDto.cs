using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class CreateOrderItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int RentalDays { get; set; }

        [Range(0, double.MaxValue)]
        public decimal DailyRate { get; set; }
    }
}
