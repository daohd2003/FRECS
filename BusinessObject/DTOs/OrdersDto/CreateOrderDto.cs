using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class CreateOrderDto
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        public Guid ProviderId { get; set; }

        [Required]
        [EnumDataType(typeof(OrderStatus))]
        public OrderStatus Status { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public DateTime? RentalStart { get; set; }
        public DateTime? RentalEnd { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required.")]
        public List<CreateOrderItemDto> Items { get; set; }
    }
}
