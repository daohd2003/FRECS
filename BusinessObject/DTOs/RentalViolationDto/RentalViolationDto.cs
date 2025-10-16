using BusinessObject.Enums;
using System;
using System.Collections.Generic;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cơ bản cho RentalViolation
    /// </summary>
    public class RentalViolationDto
    {
        public Guid ViolationId { get; set; }
        public Guid OrderItemId { get; set; }
        public ViolationType ViolationType { get; set; }
        public string Description { get; set; }
        public decimal? DamagePercentage { get; set; }
        public decimal PenaltyPercentage { get; set; }
        public decimal PenaltyAmount { get; set; }
        public ViolationStatus Status { get; set; }
        public string? CustomerNotes { get; set; }
        public DateTime? CustomerResponseAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Số lượng ảnh/video bằng chứng
        /// </summary>
        public int EvidenceCount { get; set; }
    }
}