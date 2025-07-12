using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderListDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } // Map from Id for display like DEL001, ORD001
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public List <OrderItemListDto> Items { get; set; }
        public string DeliveryAddress { get; set; }
        public string Phone { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
