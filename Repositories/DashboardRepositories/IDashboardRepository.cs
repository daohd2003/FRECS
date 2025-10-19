using BusinessObject.DTOs.DashboardStatsDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.DashboardRepositories
{
    public interface IDashboardRepository
    {
        Task<AdminDashboardDto> GetAdminDashboardDataAsync(DateTime startDate, DateTime endDate);
        Task<KPIMetrics> GetKPIMetricsAsync(DateTime startDate, DateTime endDate);
        Task<RevenueMetrics> GetRevenueMetricsAsync(DateTime startDate, DateTime endDate);
        Task<UserMetrics> GetUserMetricsAsync(DateTime startDate, DateTime endDate);
        Task<ProductMetrics> GetProductMetricsAsync(DateTime startDate, DateTime endDate);
        Task<OrderMetrics> GetOrderMetricsAsync(DateTime startDate, DateTime endDate);
        Task<SystemHealthMetrics> GetSystemHealthMetricsAsync();
        Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10);
        Task<List<TopProviderDto>> GetTopProvidersAsync(DateTime startDate, DateTime endDate, int limit = 5);
        Task<List<PopularProductDto>> GetPopularProductsAsync(DateTime startDate, DateTime endDate, int limit = 5);
        Task<List<DailyRevenueDto>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate);
        Task<PaymentMethodDistribution> GetPaymentMethodDistributionAsync(DateTime startDate, DateTime endDate);
        Task<TransactionStatusDistribution> GetTransactionStatusDistributionAsync(DateTime startDate, DateTime endDate);
        
        // Detail methods for modal
        Task<List<ProductDetailItem>> GetProductDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<OrderDetailItem>> GetOrderDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<ReportDetailItem>> GetReportDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<ViolationDetailItem>> GetViolationDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
        Task<List<UserDetailItem>> GetUserDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search);
    }
}

