using BusinessObject.DTOs.DashboardStatsDto;
using Repositories.DashboardRepositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.DashboardServices
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;

        public DashboardService(IDashboardRepository dashboardRepository)
        {
            _dashboardRepository = dashboardRepository;
        }

        public async Task<AdminDashboardDto> GetAdminDashboardDataAsync(DashboardFilterDto filter)
        {
            return await _dashboardRepository.GetAdminDashboardDataAsync(filter.StartDate, filter.EndDate);
        }

        public async Task<List<ProductDetailItem>> GetProductDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            return await _dashboardRepository.GetProductDetailsAsync(filter, startDate, endDate, search);
        }

        public async Task<List<OrderDetailItem>> GetOrderDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            return await _dashboardRepository.GetOrderDetailsAsync(filter, startDate, endDate, search);
        }

        public async Task<List<ReportDetailItem>> GetReportDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            return await _dashboardRepository.GetReportDetailsAsync(filter, startDate, endDate, search);
        }

        public async Task<List<ViolationDetailItem>> GetViolationDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            return await _dashboardRepository.GetViolationDetailsAsync(filter, startDate, endDate, search);
        }

        public async Task<List<UserDetailItem>> GetUserDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            return await _dashboardRepository.GetUserDetailsAsync(filter, startDate, endDate, search);
        }
    }
}

