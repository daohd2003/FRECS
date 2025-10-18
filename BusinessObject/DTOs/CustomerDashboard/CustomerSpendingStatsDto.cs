namespace BusinessObject.DTOs.CustomerDashboard
{
    public class CustomerSpendingStatsDto
    {
        public decimal ThisMonthSpending { get; set; }
        public int OrdersCount { get; set; }
        public decimal TotalSpent { get; set; }
        public string FavoriteCategory { get; set; }
        public int FavoriteCategoryRentalCount { get; set; }
        public decimal SpendingChangePercentage { get; set; }
        public decimal OrdersChangePercentage { get; set; }
        public decimal TotalSpentChangePercentage { get; set; }
    }
}

