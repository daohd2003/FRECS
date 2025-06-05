using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderWithDetailsDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid ProviderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime RentalStart { get; set; }
        public DateTime RentalEnd { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderStatus Status { get; set; }

        public UserDto Customer { get; set; }
        public UserDto Provider { get; set; }
        public List<OrderItemDto> Items { get; set; }
    }
}
