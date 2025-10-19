using System;

namespace BusinessObject.DTOs.DashboardStatsDto
{
    public class DashboardFilterDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Preset { get; set; } = "Last 30 Days";
    }
}

