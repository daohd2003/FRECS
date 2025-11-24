using System;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.IssueResolutionDto
{
    /// <summary>
    /// DTO trả về thông tin chi tiết về quyết định của Admin
    /// </summary>
    public class IssueResolutionResponseDto
    {
        public Guid Id { get; set; }
        public Guid ViolationId { get; set; }
        public decimal CustomerFineAmount { get; set; }
        public decimal ProviderCompensationAmount { get; set; }
        public ResolutionType ResolutionType { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ResolutionStatus ResolutionStatus { get; set; }
        public DateTime ProcessedAt { get; set; }
        public Guid ProcessedByAdminId { get; set; }
        public string ProcessedByAdminName { get; set; } = string.Empty;
    }
}

