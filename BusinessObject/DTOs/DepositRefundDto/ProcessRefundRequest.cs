using System;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.DepositRefundDto
{
    /// <summary>
    /// Request DTO khi admin approve/reject refund
    /// </summary>
    public class ProcessRefundRequest
    {
        [Required]
        public Guid RefundId { get; set; }
        
        [Required]
        public bool IsApproved { get; set; }
        
        [StringLength(100)]
        public string? RefundMethod { get; set; }
        
        [StringLength(200)]
        public string? RefundReferenceCode { get; set; }
        
        [StringLength(1000)]
        public string? AdminNotes { get; set; }
    }
}

