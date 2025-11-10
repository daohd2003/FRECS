namespace BusinessObject.DTOs.DepositDto
{
    public class RefundAccountDto
    {
        public Guid Id { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolder { get; set; }
        public bool IsPrimary { get; set; }
    }
}

