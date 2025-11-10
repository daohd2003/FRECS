namespace BusinessObject.DTOs.CustomerDashboard
{
    public class SpendingByCategoryDto
    {
        public string CategoryName { get; set; }
        public decimal TotalSpending { get; set; }
        public int OrderCount { get; set; }
    }
}

