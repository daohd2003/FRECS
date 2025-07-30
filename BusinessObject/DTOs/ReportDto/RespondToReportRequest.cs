using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.Enums;

namespace BusinessObject.DTOs.ReportDto
{
    public class RespondToReportRequest
    {
        public string ResponseMessage { get; set; }
        public ReportStatus NewStatus { get; set; }
    }
}
