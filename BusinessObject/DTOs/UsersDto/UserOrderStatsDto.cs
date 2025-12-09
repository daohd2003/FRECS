using System;

namespace BusinessObject.DTOs.UsersDto
{
    public class UserOrderStatsDto
    {
        public int TotalOrders { get; set; }
        public OrdersByStatusDto OrdersByStatus { get; set; }
        public ReturnedOrdersBreakdownDto ReturnedOrdersBreakdown { get; set; }
    }

    public class OrdersByStatusDto
    {
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int InTransit { get; set; }
        public int InUse { get; set; }
        public int Returning { get; set; }
        public int Returned { get; set; }
        public int Cancelled { get; set; }
        public int ReturnedWithIssue { get; set; }
    }

    public class ReturnedOrdersBreakdownDto
    {
        public int RentalProductsCount { get; set; }
        public decimal RentalTotalEarnings { get; set; }
        public int PurchaseProductsCount { get; set; }
        public decimal PurchaseTotalEarnings { get; set; }
        public decimal TotalEarnings { get; set; }
        public int RentalOrdersCount { get; set; }
        public int PurchaseOrdersCount { get; set; }
    }

    public class UserWithOrderStatsDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public object Profile { get; set; }
        public int TotalOrders { get; set; }
        public OrdersByStatusDto OrdersByStatus { get; set; }
        public ReturnedOrdersBreakdownDto ReturnedOrdersBreakdown { get; set; }
    }
}
