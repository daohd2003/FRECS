using BusinessObject.Enums;
using System;

namespace BusinessObject.DTOs.TransactionDto
{
    /// <summary>
    /// DTO cho việc filter và search transactions
    /// </summary>
    public class TransactionFilterDto
    {
        public string? SearchQuery { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public TransactionCategory? Category { get; set; }
        public TransactionStatus? Status { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProviderId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
