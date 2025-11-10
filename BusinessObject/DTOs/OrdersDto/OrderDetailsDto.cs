using BusinessObject.Enums;
using System.Text.Json.Serialization;

namespace BusinessObject.DTOs.OrdersDto
{
    public class OrderDetailsDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; }
        public Guid ProviderId { get; set; } // Provider who owns this order
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime CreatedAt { get; set; } // Order Placed date
        public DateTime? PaymentConfirmedDate { get; set; } // Payment confirmed date from Transaction
        public DateTime? DeliveredDate { get; set; } // Order shipped/delivered date

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OrderStatus Status { get; set; }

        public List<OrderItemDetailsDto> Items { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Tax { get; set; }
        public decimal TotalDepositAmount { get; set; } = 0m; // Tổng tiền cọc cho các items thuê
        public decimal DiscountAmount { get; set; } = 0m; // Số tiền giảm giá
        public Guid? DiscountCodeId { get; set; } // ID mã giảm giá (nếu có)
        public string? DiscountCodeName { get; set; } // Tên mã giảm giá (nếu có)
        public decimal TotalAmount { get; set; }
        public ShippingAddressDto ShippingAddress { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? EstimatedDelivery { get; set; }
        public string PaymentMethod { get; set; }
        public string? Notes { get; set; }
    }
}
