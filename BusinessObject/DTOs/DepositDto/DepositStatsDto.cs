namespace BusinessObject.DTOs.DepositDto
{
    public class DepositStatsDto
    {
        public decimal DepositsRefunded { get; set; }
        public decimal PendingRefunds { get; set; }
        public int RefundIssues { get; set; }
    }
}

