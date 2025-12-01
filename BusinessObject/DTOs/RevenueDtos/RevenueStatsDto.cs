using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
        
        // Net Revenue (After platform fees)
        public decimal NetRevenue { get; set; }
        public decimal PreviousNetRevenue { get; set; }
        public decimal NetRevenueGrowthPercentage { get; set; }
        
        // Net Revenue Breakdown
        public decimal NetRevenueFromOrders { get; set; }
        public decimal NetRevenueFromPenalties { get; set; }
        public decimal PreviousNetRevenueFromOrders { get; set; }
        public decimal PreviousNetRevenueFromPenalties { get; set; }
        
        // Platform Commission Fee
        public decimal PlatformFee { get; set; }
        public decimal PreviousPlatformFee { get; set; }
        public decimal PlatformFeeGrowthPercentage { get; set; }
        
        // Breakdown by transaction type
        public decimal RentalRevenue { get; set; }
        public decimal PurchaseRevenue { get; set; }
        public decimal RentalFee { get; set; }  // 20%
        public decimal PurchaseFee { get; set; }  // 10%
        
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
        public string? RoutingNumber { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Last4Digits => AccountNumber?.Length >= 4 ? AccountNumber.Substring(AccountNumber.Length - 4) : AccountNumber;
    }

    public class CreateBankAccountDto
    {
        [Required(ErrorMessage = "Bank name is required.")]
        [StringLength(255, ErrorMessage = "Bank name cannot exceed 255 characters.")]
        [NotOnlyWhitespace(ErrorMessage = "Bank name cannot contain only whitespace.")]
        public string BankName { get; set; }

        [Required(ErrorMessage = "Account number is required.")]
        [StringLength(17, MinimumLength = 8, ErrorMessage = "Account number must be between 8 and 17 digits.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter a valid account number (digits only).")]
        public string AccountNumber { get; set; }

        [Required(ErrorMessage = "Account holder name is required.")]
        [StringLength(255, ErrorMessage = "Account holder name cannot exceed 255 characters.")]
        [NotOnlyWhitespace(ErrorMessage = "Account holder name cannot contain only whitespace.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name cannot contain special characters.")]
        public string AccountHolderName { get; set; }

        [StringLength(50, ErrorMessage = "Routing number cannot exceed 50 characters.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter a valid routing number (digits only).")]
        public string? RoutingNumber { get; set; }

        public bool SetAsPrimary { get; set; } = false;
    }

    // Custom validation attribute to check for whitespace-only strings
    public class NotOnlyWhitespaceAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage ?? "Field cannot contain only whitespace.");
        }
    }

    public class TopRevenueItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public string TransactionType { get; set; } = string.Empty; // "rental" or "purchase"
    }

    public class TopCustomerDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerAvatarUrl { get; set; }
        public decimal TotalSpent { get; set; }
        public int OrderCount { get; set; }
    }
}
