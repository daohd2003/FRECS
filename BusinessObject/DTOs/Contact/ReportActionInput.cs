using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.Contact
{
    public class ReportActionInput
    {
        public string Action { get; set; } = default!;
    public Guid ReportId { get; set; }
    public ReportStatus? NewStatus { get; set; }   // Cho phép null
    public Guid? NewAdminId { get; set; }          // Cho phép null
    public string? ResponseMessage { get; set; }
    }
}
