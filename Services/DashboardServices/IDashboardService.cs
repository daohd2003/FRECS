using BusinessObject.DTOs.DashboardStatsDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.DashboardServices
{
    public interface IDashboardService
    {
        Task<AdminDashboardDto> GetAdminDashboardDataAsync(DashboardFilterDto filter);
        Task<List<ProductDetailItem>> GetProductDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<OrderDetailItem>> GetOrderDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<ReportDetailItem>> GetReportDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<ViolationDetailItem>> GetViolationDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<UserDetailItem>> GetUserDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
    }
}

