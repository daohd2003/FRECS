using System;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.OrdersDto
{
    public class AdminOrderListDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string ProviderName { get; set; }
        public string ProviderEmail { get; set; }
        public string TransactionType { get; set; } // rental/purchase
        public DateTime? RentalStartDate { get; set; }
        public DateTime? RentalEndDate { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
