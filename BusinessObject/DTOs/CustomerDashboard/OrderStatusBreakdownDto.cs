namespace BusinessObject.DTOs.CustomerDashboard
{
    public class OrderStatusBreakdownDto
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}

