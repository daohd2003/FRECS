using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.RevenueDtos
{
    public class RevenueStatsDto
    {
        public decimal CurrentPeriodRevenue { get; set; }
        public decimal PreviousPeriodRevenue { get; set; }
        public decimal RevenueGrowthPercentage { get; set; }
        public int CurrentPeriodOrders { get; set; }
        public int PreviousPeriodOrders { get; set; }
        public decimal OrderGrowthPercentage { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal PreviousAverageOrderValue { get; set; }
        public decimal AvgOrderValueGrowthPercentage { get; set; }
        
        // New metrics for Provider
        public int ActiveListings { get; set; }
        public int PreviousActiveListings { get; set; }
        public decimal ActiveListingsGrowthPercentage { get; set; }
        public decimal CustomerRating { get; set; }
        public decimal PreviousCustomerRating { get; set; }
        public decimal CustomerRatingGrowthPercentage { get; set; }
        
        // New metrics for Customer
        public decimal TotalSpent { get; set; }
        public decimal PreviousTotalSpent { get; set; }
        public decimal TotalSpentGrowthPercentage { get; set; }
        public string FavoriteCategory { get; set; } = string.Empty;
        public string PreviousFavoriteCategory { get; set; } = string.Empty;
        public int FavoriteCategoryCount { get; set; }
        
        public List<RevenueChartDataDto> ChartData { get; set; } = new List<RevenueChartDataDto>();
        public List<OrderStatusBreakdownDto> StatusBreakdown { get; set; } = new List<OrderStatusBreakdownDto>();
    }

    public class RevenueChartDataDto
    {
        public string Period { get; set; } // "2024-01", "Week 1", "2024-01-01"
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public DateTime Date { get; set; }
    }

    public class OrderStatusBreakdownDto
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public decimal Revenue { get; set; }
    }

    public class PayoutSummaryDto
    {
        public decimal CurrentBalance { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal TotalPayouts { get; set; }
        public DateTime? NextPayoutDate { get; set; }
        public List<PayoutHistoryDto> RecentPayouts { get; set; } = new List<PayoutHistoryDto>();
    }

    public class PayoutHistoryDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } // "completed", "pending", "failed"
        public string BankAccountLast4 { get; set; }
        public string TransactionId { get; set; }
    }

    public class BankAccountDto
    {
        public Guid Id { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolderName { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Last4Digits => AccountNumber?.Length >= 4 ? AccountNumber.Substring(AccountNumber.Length - 4) : AccountNumber;
    }

    public class CreateBankAccountDto
    {
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolderName { get; set; }
        public bool SetAsPrimary { get; set; } = false;
    }
}
