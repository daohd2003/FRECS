using System;
using System.Collections.Generic;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.IssueResolutionDto
{
    /// <summary>
    /// DTO chi tiết cho một vụ tranh chấp, bao gồm tất cả thông tin cần thiết cho Admin
    /// </summary>
    public class DisputeCaseDetailDto
    {
        public Guid ViolationId { get; set; }
        public ViolationType ViolationType { get; set; }
        public string DamageDescription { get; set; } = string.Empty;
        public decimal? DamagePercentage { get; set; }
        public decimal RequestedCompensation { get; set; }
        public decimal CurrentPenaltyAmount { get; set; }
        public ViolationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CustomerNotes { get; set; }
        public DateTime? CustomerResponseAt { get; set; }
        public string? ProviderEscalationReason { get; set; }
        public string? CustomerEscalationReason { get; set; }

        // Product Info
        public ProductInfoDto Product { get; set; } = new();

        // Provider Info
        public UserInfoDto Provider { get; set; } = new();

        // Customer Info
        public UserInfoDto Customer { get; set; } = new();

        // Evidence
        public List<EvidenceDto> ProviderEvidence { get; set; } = new();
        public List<EvidenceDto> CustomerEvidence { get; set; } = new();

        // Communication History
        public List<MessageDto> Messages { get; set; } = new();

        // Order Item Info
        public OrderItemInfoDto OrderItem { get; set; } = new();
    }

    public class ProductInfoDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string CompensationPolicy { get; set; } = string.Empty;
    }

    public class UserInfoDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
    }

    public class EvidenceDto
    {
        public Guid ImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public DateTime UploadedAt { get; set; }
        public EvidenceUploadedBy UploadedBy { get; set; }
    }

    public class MessageDto
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public MessageAttachmentDto? Attachment { get; set; }
    }

    public class MessageAttachmentDto
    {
        public string Url { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? MimeType { get; set; }
        public string? FileName { get; set; }
    }

    public class OrderItemInfoDto
    {
        public Guid OrderItemId { get; set; }
        public Guid OrderId { get; set; }
        public int Quantity { get; set; }
        public decimal DepositPerUnit { get; set; }
        public decimal TotalDeposit { get; set; }
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
    }
}

