namespace BusinessObject.DTOs.CustomerDashboard
{
    public class CustomerSpendingStatsDto
    {
        // Spending for current period (Subtotal - Discount, excluding deposit and penalty)
        public decimal ThisPeriodSpending { get; set; }
        
        public int OrdersCount { get; set; }
        
        // Penalty paid for current period (from RentalViolations with CUSTOMER_ACCEPTED or RESOLVED status)
        public decimal PenaltyPaidThisPeriod { get; set; }
        
        // Total amounts all time (for separate display, not period-based)
        public decimal TotalRentalPurchaseAllTime { get; set; }  // Subtotal - Discount
        public decimal TotalDepositedAllTime { get; set; }       // All deposits paid by customer
        public decimal TotalPenaltiesAllTime { get; set; }       // All penalties paid
        
        public string FavoriteCategory { get; set; }
        public int FavoriteCategoryRentalCount { get; set; }
        
        public decimal SpendingChangePercentage { get; set; }
        public decimal OrdersChangePercentage { get; set; }
        public decimal PenaltyPaidChangePercentage { get; set; }
    }
}

