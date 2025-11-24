using System;

namespace BusinessObject.DTOs.IssueResolutionDto
{
    /// <summary>
    /// DTO để hiển thị danh sách các tranh chấp chờ xử lý
    /// </summary>
    public class DisputeCaseListDto
    {
        public Guid ViolationId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public Guid ProviderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public DateTime ComplaintDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RequestedCompensation { get; set; }
    }
}

