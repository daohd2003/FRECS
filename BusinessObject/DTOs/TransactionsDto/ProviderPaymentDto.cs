namespace BusinessObject.DTOs.TransactionsDto
{
    public class ProviderPaymentDto
    {
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderEmail { get; set; } = string.Empty;
        public decimal TotalEarned { get; set; }
        public int CompletedOrders { get; set; }
        public DateTime? LastPayment { get; set; }
        public string? BankAccount { get; set; }
        public string? BankName { get; set; }
    }

    public class AllProvidersPaymentSummaryDto
    {
        public decimal TotalAmountOwed { get; set; }
        public int TotalProviders { get; set; }
        public IEnumerable<ProviderPaymentDto> Providers { get; set; } = new List<ProviderPaymentDto>();
    }
}
