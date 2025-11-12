using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.ReportDto
{
    public class ReportViewModel
    {
        public Guid Id { get; set; }
        public Guid ReporterId { get; set; }
        public Guid? ReporteeId { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public string ReporterName { get; set; }
        public string ReporterEmail { get; set; }
        public string ReporteeName { get; set; }
        public string ReporteeEmail { get; set; }
        public DateTime DateCreated { get; set; }
        public string Status { get; set; }
        public ReportPriority Priority { get; set; }
        public Guid? AssignedAdminId { get; set; }
        public string? AssignedAdminName { get; set; }
        public string? AdminResponse { get; set; }
        
        // Order-related fields
        public Guid? OrderId { get; set; }
        public ReportType ReportType { get; set; }
        public string? OrderCode { get; set; }
        public List<OrderProductInfo>? OrderProducts { get; set; }
    }
    
    public class OrderProductInfo
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
