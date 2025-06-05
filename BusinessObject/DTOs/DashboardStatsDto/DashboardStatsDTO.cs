using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.DashboardStatsDto
{
    public class DashboardStatsDTO
    {
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int InUseCount { get; set; }
        public int ReturnedCount { get; set; }
        public int CancelledCount { get; set; }

        // Có thể bổ sung nếu cần
        public int TotalOrders => PendingCount + ApprovedCount + InUseCount + ReturnedCount + CancelledCount;
    }
}
