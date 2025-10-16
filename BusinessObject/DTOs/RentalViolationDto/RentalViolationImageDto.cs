using BusinessObject.Enums;
using System;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cho ảnh/video bằng chứng
    /// </summary>
    public class RentalViolationImageDto
    {
        public Guid ImageId { get; set; }
        public Guid ViolationId { get; set; }
        public string ImageUrl { get; set; }
        public EvidenceUploadedBy UploadedBy { get; set; }
        public string UploadedByDisplay { get; set; } // "Nhà cung cấp" hoặc "Khách hàng"
        public string? FileType { get; set; } // "image" hoặc "video"
        public DateTime UploadedAt { get; set; }
    }
}