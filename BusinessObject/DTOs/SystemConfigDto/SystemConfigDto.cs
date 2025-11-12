using System;

namespace BusinessObject.DTOs.SystemConfigDto
{
    public class SystemConfigDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedByAdminId { get; set; }
    }

    public class UpdateSystemConfigDto
    {
        public string Value { get; set; }
        public string? Description { get; set; }
    }

    public class CommissionRatesDto
    {
        public decimal RentalCommissionRate { get; set; }
        public decimal PurchaseCommissionRate { get; set; }
        public DateTime LastUpdated { get; set; }
        public Guid? UpdatedByAdminId { get; set; }
    }
}
