using BusinessObject.Enums;

namespace BusinessObject.DTOs.DepositDto
{
    public class DepositHistoryDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; }
        public string ItemName { get; set; }
        public decimal DepositAmount { get; set; }
        public string RefundMethod { get; set; }
        public decimal RefundAmount { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime? RefundDate { get; set; }
    }
}

