using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.DashboardStatsDto
{
    public class AdminDashboardDto
    {
        public KPIMetrics KPIs { get; set; } = new();
        public RevenueMetrics Revenue { get; set; } = new();
        public UserMetrics Users { get; set; } = new();
        public ProductMetrics Products { get; set; } = new();
        public OrderMetrics Orders { get; set; } = new();
        public SystemHealthMetrics SystemHealth { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<TopProviderDto> TopProviders { get; set; } = new();
        public List<PopularProductDto> PopularProducts { get; set; } = new();
        public List<DailyRevenueDto> DailyRevenue { get; set; } = new();
        public PaymentMethodDistribution PaymentMethods { get; set; } = new();
        public TransactionStatusDistribution TransactionStatus { get; set; } = new();
    }

    public class KPIMetrics
    {
        public decimal TotalRevenue { get; set; }
        public decimal RevenueChange { get; set; }
        public int TotalOrders { get; set; }
        public decimal OrdersChange { get; set; }
        public int TotalUsers { get; set; }
        public decimal UsersChange { get; set; }
        public int ActiveProducts { get; set; }
        public decimal ProductsChange { get; set; }
    }

    public class RevenueMetrics
    {
        public decimal TotalRevenue { get; set; }
        public decimal RentalRevenue { get; set; }
        public decimal PurchaseRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int SuccessfulTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public decimal SuccessRate { get; set; }
    }

    public class UserMetrics
    {
        public int TotalUsers { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProviders { get; set; }
        public int TotalStaff { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int ActiveUsersToday { get; set; }
        public int PendingProviderApplications { get; set; }
    }

    public class ProductMetrics
    {
        public int TotalProducts { get; set; }
        public int AvailableProducts { get; set; }
        public int RentedProducts { get; set; }
        public int UnavailableProducts { get; set; }
        public int TotalSoldItems { get; set; }
        public int TotalReviews { get; set; }
        public int NewProductsThisMonth { get; set; }
    }

    public class OrderMetrics
    {
        public int TotalOrders { get; set; }
        public int InUseOrders { get; set; }
        public int ApprovedOrders { get; set; }
        public int ReturnedWithIssueOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal CompletionRate { get; set; }
        public decimal CancellationRate { get; set; }
    }

    public class SystemHealthMetrics
    {
        public int PendingReports { get; set; }
        public int UnresolvedReports { get; set; }
        public int ActiveViolations { get; set; }
        public int PendingVerifications { get; set; }
        public decimal SystemUptime { get; set; }
        public int BannedUsers { get; set; }
        public decimal AverageResponseTime { get; set; }
    }

    public class RecentActivityDto
    {
        public string Type { get; set; } = string.Empty; // Order, User, Product, Report, etc.
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public Guid? EntityId { get; set; } // ID of the related entity (Order, User, Product, Report)
        public string NavigationUrl { get; set; } = string.Empty; // URL to navigate to
    }

    public class TopProviderDto
    {
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalProducts { get; set; }
    }

    public class PopularProductDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public int RentCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageRating { get; set; }
        public decimal PricePerDay { get; set; }
    }

    public class DailyRevenueDto
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class PaymentMethodDistribution
    {
        public int VNPay { get; set; }
        public int SEPay { get; set; }
    }

    public class TransactionStatusDistribution
    {
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Pending { get; set; }
        public int Initiated { get; set; }
    }

    // Detail Item DTOs for Modal
    public class ProductDetailItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public decimal PricePerDay { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class OrderDetailItem
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ReportDetailItem
    {
        public Guid Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string ReporterName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ViolationDetailItem
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? FineAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserDetailItem
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

