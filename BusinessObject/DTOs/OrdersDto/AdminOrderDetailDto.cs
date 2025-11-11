using System;
using System.Collections.Generic;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.OrdersDto
{
    public class AdminOrderDetailDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; }
        public OrderStatus Status { get; set; }
        public string TransactionType { get; set; } // "rental" or "purchase"
        public DateTime CreatedAt { get; set; }
        
        // Customer Information
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        
        // Provider Information
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string ProviderEmail { get; set; }
        public string ProviderPhone { get; set; }
        
        // Rental Information (if applicable)
        public DateTime? RentalStartDate { get; set; }
        public DateTime? RentalEndDate { get; set; }
        public int? RentalDays { get; set; }
        
        // Order Items
        public List<AdminOrderItemDto> OrderItems { get; set; }
        
        // Financial Information
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        
        // Payment Information
        public string PaymentMethod { get; set; }
        public bool IsPaid { get; set; }
        
        // Additional Information
        public string Note { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    
    public class AdminOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public string Color { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string TransactionType { get; set; } // "rental" or "purchase"
        public decimal? TotalDeposit { get; set; } // Total deposit for rental items
        public int? RentalDays { get; set; } // Rental duration in days
    }
}
