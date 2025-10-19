using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.DashboardRepositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly ShareItDbContext _context;

        public DashboardRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardDto> GetAdminDashboardDataAsync(DateTime startDate, DateTime endDate)
        {
            var dashboard = new AdminDashboardDto
            {
                KPIs = await GetKPIMetricsAsync(startDate, endDate),
                Revenue = await GetRevenueMetricsAsync(startDate, endDate),
                Users = await GetUserMetricsAsync(startDate, endDate),
                Products = await GetProductMetricsAsync(startDate, endDate),
                Orders = await GetOrderMetricsAsync(startDate, endDate),
                SystemHealth = await GetSystemHealthMetricsAsync(),
                RecentActivities = await GetRecentActivitiesAsync(10),
                TopProviders = await GetTopProvidersAsync(startDate, endDate, 5),
                PopularProducts = await GetPopularProductsAsync(startDate, endDate, 5),
                DailyRevenue = await GetDailyRevenueAsync(startDate, endDate),
                PaymentMethods = await GetPaymentMethodDistributionAsync(startDate, endDate),
                TransactionStatus = await GetTransactionStatusDistributionAsync(startDate, endDate)
            };

            return dashboard;
        }

        public async Task<KPIMetrics> GetKPIMetricsAsync(DateTime startDate, DateTime endDate)
        {
            var previousStartDate = startDate.AddDays(-(endDate - startDate).Days);
            var previousEndDate = startDate;

            // Current period
            var currentRevenue = await _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate && t.Status == TransactionStatus.completed)
                .SumAsync(t => t.Amount);

            var currentOrders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .CountAsync();

            var currentUsers = await _context.Users
                .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
                .CountAsync();

            var currentProducts = await _context.Products
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .CountAsync();

            // Previous period
            var previousRevenue = await _context.Transactions
                .Where(t => t.TransactionDate >= previousStartDate && t.TransactionDate < previousEndDate && t.Status == TransactionStatus.completed)
                .SumAsync(t => t.Amount);

            var previousOrders = await _context.Orders
                .Where(o => o.CreatedAt >= previousStartDate && o.CreatedAt < previousEndDate)
                .CountAsync();

            var previousUsers = await _context.Users
                .Where(u => u.CreatedAt >= previousStartDate && u.CreatedAt < previousEndDate)
                .CountAsync();

            var previousProducts = await _context.Products
                .Where(p => p.CreatedAt >= previousStartDate && p.CreatedAt < previousEndDate)
                .CountAsync();

            return new KPIMetrics
            {
                TotalRevenue = currentRevenue,
                RevenueChange = previousRevenue > 0 ? ((currentRevenue - previousRevenue) / previousRevenue) * 100 : 0,
                TotalOrders = currentOrders,
                OrdersChange = previousOrders > 0 ? ((decimal)(currentOrders - previousOrders) / (decimal)previousOrders * 100) : 0,
                TotalUsers = currentUsers,
                UsersChange = previousUsers > 0 ? ((decimal)(currentUsers - previousUsers) / (decimal)previousUsers * 100) : 0,
                ActiveProducts = currentProducts,
                ProductsChange = previousProducts > 0 ? ((decimal)(currentProducts - previousProducts) / (decimal)previousProducts * 100) : 0
            };
        }

        public async Task<RevenueMetrics> GetRevenueMetricsAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .ToListAsync();

            var completedTransactions = transactions.Where(t => t.Status == TransactionStatus.completed).ToList();
            var totalRevenue = completedTransactions.Sum(t => t.Amount);

            var successfulCount = completedTransactions.Count;
            var failedCount = transactions.Count(t => t.Status == TransactionStatus.failed);
            var totalTransactions = transactions.Count;

            var orders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToListAsync();

            return new RevenueMetrics
            {
                TotalRevenue = totalRevenue,
                RentalRevenue = totalRevenue * 0.7m, // Estimate - you can calculate based on order types
                PurchaseRevenue = totalRevenue * 0.3m, // Estimate
                AverageOrderValue = orders.Any() ? orders.Average(o => o.TotalAmount) : 0,
                SuccessfulTransactions = successfulCount,
                FailedTransactions = failedCount,
                SuccessRate = totalTransactions > 0 ? ((decimal)successfulCount / totalTransactions) * 100 : 0
            };
        }

        public async Task<UserMetrics> GetUserMetricsAsync(DateTime startDate, DateTime endDate)
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalCustomers = await _context.Users.Where(u => u.Role == UserRole.customer).CountAsync();
            var totalProviders = await _context.Users.Where(u => u.Role == UserRole.provider).CountAsync();
            var totalStaff = await _context.Users.Where(u => u.Role == UserRole.staff || u.Role == UserRole.admin).CountAsync();

            var newUsersThisMonth = await _context.Users
                .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
                .CountAsync();

            var today = DateTime.UtcNow.Date;
            var activeUsersToday = await _context.Users
                .Where(u => u.LastLogin.HasValue && u.LastLogin.Value.Date == today)
                .CountAsync();

            var pendingApplications = await _context.ProviderApplications
                .Where(p => p.Status == ProviderApplicationStatus.pending)
                .CountAsync();

            return new UserMetrics
            {
                TotalUsers = totalUsers,
                TotalCustomers = totalCustomers,
                TotalProviders = totalProviders,
                TotalStaff = totalStaff,
                NewUsersThisMonth = newUsersThisMonth,
                ActiveUsersToday = activeUsersToday,
                PendingProviderApplications = pendingApplications
            };
        }

        public async Task<ProductMetrics> GetProductMetricsAsync(DateTime startDate, DateTime endDate)
        {
            var totalProducts = await _context.Products.CountAsync();
            var availableProducts = await _context.Products.Where(p => p.AvailabilityStatus == AvailabilityStatus.available).CountAsync();
            
            // Count products currently being rented (unique products in orders with status = in_use)
            var rentedProducts = await _context.OrderItems
                .Where(oi => oi.Order.Status == OrderStatus.in_use && 
                            oi.TransactionType == BusinessObject.Enums.TransactionType.rental)
                .Select(oi => oi.ProductId)
                .Distinct()
                .CountAsync();
            
            var unavailableProducts = await _context.Products.Where(p => p.AvailabilityStatus == AvailabilityStatus.unavailable).CountAsync();

            // Calculate total purchased items (unique products that have been purchased)
            var totalSoldItems = await _context.OrderItems
                .Where(oi => oi.Order.Status == OrderStatus.returned && 
                            oi.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                .Select(oi => oi.ProductId)
                .Distinct()
                .CountAsync();
            
            var totalReviews = await _context.Feedbacks.CountAsync();

            var newProducts = await _context.Products
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .CountAsync();

            return new ProductMetrics
            {
                TotalProducts = totalProducts,
                AvailableProducts = availableProducts,
                RentedProducts = rentedProducts,
                UnavailableProducts = unavailableProducts,
                TotalSoldItems = totalSoldItems,
                TotalReviews = totalReviews,
                NewProductsThisMonth = newProducts
            };
        }

        public async Task<OrderMetrics> GetOrderMetricsAsync(DateTime startDate, DateTime endDate)
        {
            // Count ALL orders (not filtered by date range) to show current state
            var allOrders = await _context.Orders.ToListAsync();

            var totalOrders = allOrders.Count; // All orders regardless of status
            var inUseOrders = allOrders.Count(o => o.Status == OrderStatus.in_use);
            var approvedOrders = allOrders.Count(o => o.Status == OrderStatus.approved);
            var returnedWithIssueOrders = allOrders.Count(o => o.Status == OrderStatus.returned_with_issue);
            var completedOrders = allOrders.Count(o => o.Status == OrderStatus.returned); // "returned" = completed
            var cancelledOrders = allOrders.Count(o => o.Status == OrderStatus.cancelled);

            return new OrderMetrics
            {
                TotalOrders = totalOrders,
                InUseOrders = inUseOrders,
                ApprovedOrders = approvedOrders,
                ReturnedWithIssueOrders = returnedWithIssueOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                CompletionRate = totalOrders > 0 ? ((decimal)completedOrders / totalOrders) * 100 : 0,
                CancellationRate = totalOrders > 0 ? ((decimal)cancelledOrders / totalOrders) * 100 : 0
            };
        }

        public async Task<SystemHealthMetrics> GetSystemHealthMetricsAsync()
        {
            var pendingReports = await _context.Reports.Where(r => r.Status == ReportStatus.open).CountAsync();
            var unresolvedReports = await _context.Reports.Where(r => r.Status != ReportStatus.resolved).CountAsync();
            var activeViolations = await _context.RentalViolations.Where(v => v.Status == ViolationStatus.PENDING || v.Status == ViolationStatus.CUSTOMER_REJECTED).CountAsync();
            
            // Count banned users (users with IsActive = false)
            var bannedUsers = await _context.Users
                .Where(u => u.IsActive == false)
                .CountAsync();

            return new SystemHealthMetrics
            {
                PendingReports = pendingReports,
                UnresolvedReports = unresolvedReports,
                ActiveViolations = activeViolations,
                PendingVerifications = 0, // Add product verification if you have it
                SystemUptime = 99.9m,
                BannedUsers = bannedUsers,
                AverageResponseTime = 2.5m // Mock data - calculate based on report response times
            };
        }

        public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10)
        {
            var activities = new List<RecentActivityDto>();

            // Recent orders
            var recentOrders = await _context.Orders
                .Include(o => o.Customer)
                .ThenInclude(c => c.Profile)
                .OrderByDescending(o => o.CreatedAt)
                .Take(3)
                .ToListAsync();

            foreach (var order in recentOrders)
            {
                activities.Add(new RecentActivityDto
                {
                    Type = "Order",
                    Description = $"New order #{order.Id.ToString().Substring(0, 8)} from {order.Customer?.Profile?.FullName ?? "Unknown"}",
                    Timestamp = order.CreatedAt,
                    Icon = "shopping-cart",
                    Color = "blue",
                    EntityId = order.Id,
                    NavigationUrl = $"/order/details/{order.Id}"
                });
            }

            // Recent users
            var recentUsers = await _context.Users
                .Include(u => u.Profile)
                .OrderByDescending(u => u.CreatedAt)
                .Take(3)
                .ToListAsync();

            foreach (var user in recentUsers)
            {
                // Phân biệt staff vs customer/provider để navigate đúng trang
                string navigationUrl;
                if (user.Role == UserRole.staff)
                {
                    navigationUrl = $"/admin/staffmanagement?staffId={user.Id}&openDetail=true";
                }
                else
                {
                    navigationUrl = $"/admin/usermanagement?userId={user.Id}&openDetail=true";
                }

                activities.Add(new RecentActivityDto
                {
                    Type = "User",
                    Description = $"New user registered: {user.Profile?.FullName ?? user.Email}",
                    Timestamp = user.CreatedAt,
                    Icon = "user-plus",
                    Color = "green",
                    EntityId = user.Id,
                    NavigationUrl = navigationUrl
                });
            }

            // Recent products
            var recentProducts = await _context.Products
                .Include(p => p.Provider)
                .ThenInclude(pr => pr.Profile)
                .OrderByDescending(p => p.CreatedAt)
                .Take(2)
                .ToListAsync();

            foreach (var product in recentProducts)
            {
                activities.Add(new RecentActivityDto
                {
                    Type = "Product",
                    Description = $"New product listed: {product.Name}",
                    Timestamp = product.CreatedAt,
                    Icon = "package",
                    Color = "purple",
                    EntityId = product.Id,
                    NavigationUrl = $"/products/detail/{product.Id}"
                });
            }

            // Recent reports
            var recentReports = await _context.Reports
                .OrderByDescending(r => r.CreatedAt)
                .Take(2)
                .ToListAsync();

            foreach (var report in recentReports)
            {
                activities.Add(new RecentActivityDto
                {
                    Type = "Report",
                    Description = $"New report: {report.Subject}",
                    Timestamp = report.CreatedAt,
                    Icon = "alert-circle",
                    Color = "red",
                    EntityId = report.Id,
                    NavigationUrl = $"/reportmanagement?reportId={report.Id}&openDetail=true"
                });
            }

            return activities.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
        }

        public async Task<List<TopProviderDto>> GetTopProvidersAsync(DateTime startDate, DateTime endDate, int limit = 5)
        {
            var topProviders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.Status == OrderStatus.returned)
                .Include(o => o.Provider)
                .ThenInclude(p => p.Profile)
                .GroupBy(o => o.ProviderId)
                .Select(g => new
                {
                    ProviderId = g.Key,
                    TotalRevenue = g.Sum(o => o.TotalAmount),
                    TotalOrders = g.Count(),
                    Provider = g.First().Provider
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(limit)
                .ToListAsync();

            var result = new List<TopProviderDto>();
            foreach (var provider in topProviders)
            {
                var products = await _context.Products.Where(p => p.ProviderId == provider.ProviderId).ToListAsync();
                var avgRating = products.Where(p => p.RatingCount > 0).Any() 
                    ? products.Where(p => p.RatingCount > 0).Average(p => p.AverageRating) 
                    : 0;

                result.Add(new TopProviderDto
                {
                    ProviderId = provider.ProviderId,
                    ProviderName = provider.Provider?.Profile?.FullName ?? "Unknown",
                    AvatarUrl = provider.Provider?.Profile?.ProfilePictureUrl,
                    TotalRevenue = provider.TotalRevenue,
                    TotalOrders = provider.TotalOrders,
                    AverageRating = avgRating,
                    TotalProducts = products.Count
                });
            }

            return result;
        }

        public async Task<List<PopularProductDto>> GetPopularProductsAsync(DateTime startDate, DateTime endDate, int limit = 5)
        {
            var popularProducts = await _context.OrderItems
                .Where(oi => oi.Order.CreatedAt >= startDate && oi.Order.CreatedAt <= endDate)
                .Include(oi => oi.Product)
                .ThenInclude(p => p.Provider)
                .ThenInclude(pr => pr.Profile)
                .Include(oi => oi.Product.Images)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    RentCount = g.Count(),
                    Revenue = g.Sum(oi => oi.DailyRate * (oi.RentalDays ?? 1) * oi.Quantity),
                    Product = g.First().Product
                })
                .OrderByDescending(x => x.RentCount)
                .Take(limit)
                .ToListAsync();

            return popularProducts.Select(p => new PopularProductDto
            {
                ProductId = p.ProductId,
                ProductName = p.Product.Name,
                ImageUrl = p.Product.Images.FirstOrDefault()?.ImageUrl,
                ProviderName = p.Product.Provider?.Profile?.FullName ?? "Unknown",
                RentCount = p.RentCount,
                Revenue = p.Revenue,
                AverageRating = p.Product.AverageRating,
                PricePerDay = p.Product.PricePerDay
            }).ToList();
        }

        public async Task<List<DailyRevenueDto>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate)
        {
            var dailyRevenue = await _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate && t.Status == TransactionStatus.completed)
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new DailyRevenueDto
                {
                    Date = g.Key,
                    Revenue = g.Sum(t => t.Amount),
                    OrderCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToListAsync();

            // Fill in missing dates with zero revenue
            var allDates = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(offset => startDate.AddDays(offset).Date)
                .ToList();

            var result = allDates.Select(date =>
            {
                var existing = dailyRevenue.FirstOrDefault(d => d.Date == date);
                return existing ?? new DailyRevenueDto { Date = date, Revenue = 0, OrderCount = 0 };
            }).ToList();

            return result;
        }

        public async Task<PaymentMethodDistribution> GetPaymentMethodDistributionAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate && t.Status == TransactionStatus.completed)
                .ToListAsync();

            return new PaymentMethodDistribution
            {
                VNPay = transactions.Count(t => t.PaymentMethod != null && t.PaymentMethod.Contains("VNPAY", StringComparison.OrdinalIgnoreCase)),
                SEPay = transactions.Count(t => t.PaymentMethod != null && t.PaymentMethod.Contains("SEPay", StringComparison.OrdinalIgnoreCase))
            };
        }

        public async Task<TransactionStatusDistribution> GetTransactionStatusDistributionAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return new TransactionStatusDistribution
            {
                Completed = transactions.FirstOrDefault(t => t.Status == TransactionStatus.completed)?.Count ?? 0,
                Failed = transactions.FirstOrDefault(t => t.Status == TransactionStatus.failed)?.Count ?? 0,
                Pending = 0, // TransactionStatus doesn't have 'pending'
                Initiated = transactions.FirstOrDefault(t => t.Status == TransactionStatus.initiated)?.Count ?? 0
            };
        }

        // Detail methods for modal
        public async Task<List<ProductDetailItem>> GetProductDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            // For detail modal, show ALL products (ignore date range) to see available inventory
            var query = _context.Products
                .Include(p => p.Provider)
                .ThenInclude(pr => pr.Profile)
                .Include(p => p.Images)
                .AsQueryable();

            // Apply filter by status
            if (!string.IsNullOrEmpty(filter) && filter != "all")
            {
                switch (filter.ToLower())
                {
                    case "available":
                        query = query.Where(p => p.AvailabilityStatus == AvailabilityStatus.available);
                        break;
                    case "rented":
                        // Products currently being rented (in active orders)
                        var rentedProductIds = await _context.OrderItems
                            .Where(oi => oi.Order.Status == OrderStatus.in_use && 
                                        oi.TransactionType == BusinessObject.Enums.TransactionType.rental)
                            .Select(oi => oi.ProductId)
                            .Distinct()
                            .ToListAsync();
                        query = query.Where(p => rentedProductIds.Contains(p.Id));
                        break;
                    case "purchased":
                    case "sold":
                        // Products that have been purchased (completed purchase orders)
                        var purchasedProductIds = await _context.OrderItems
                            .Where(oi => oi.Order.Status == OrderStatus.returned && 
                                        oi.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                            .Select(oi => oi.ProductId)
                            .Distinct()
                            .ToListAsync();
                        query = query.Where(p => purchasedProductIds.Contains(p.Id));
                        break;
                    case "unavailable":
                        query = query.Where(p => p.AvailabilityStatus == AvailabilityStatus.unavailable);
                        break;
                }
            }

            // Apply search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || 
                                        p.Provider.Profile.FullName.Contains(search));
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(100) // Limit to 100 items
                .ToListAsync();

            return products.Select(p => new ProductDetailItem
            {
                Id = p.Id,
                Name = p.Name,
                ImageUrl = p.Images.FirstOrDefault()?.ImageUrl,
                ProviderName = p.Provider?.Profile?.FullName ?? "Unknown",
                PricePerDay = p.PricePerDay,
                Status = p.AvailabilityStatus.ToString(),
                CreatedAt = p.CreatedAt
            }).ToList();
        }

        public async Task<List<OrderDetailItem>> GetOrderDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            // For detail modal, show ALL orders (ignore date range)
            var query = _context.Orders
                .Include(o => o.Customer)
                .ThenInclude(c => c.Profile)
                .Include(o => o.Provider)
                .ThenInclude(pr => pr.Profile)
                .AsQueryable();

            // Apply filter
            if (!string.IsNullOrEmpty(filter) && filter != "all")
            {
                query = filter.ToLower() switch
                {
                    "in_use" => query.Where(o => o.Status == OrderStatus.in_use),
                    "returned_with_issue" => query.Where(o => o.Status == OrderStatus.returned_with_issue),
                    "completed" => query.Where(o => o.Status == OrderStatus.returned),
                    _ => query
                };
            }

            // Apply search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.Customer.Profile.FullName.Contains(search) ||
                                        o.Provider.Profile.FullName.Contains(search) ||
                                        o.Id.ToString().Contains(search));
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(50)
                .ToListAsync();

            return orders.Select(o => new OrderDetailItem
            {
                Id = o.Id,
                OrderNumber = $"ORD-{o.Id.ToString().Substring(0, 8).ToUpper()}",
                CustomerName = o.Customer?.Profile?.FullName ?? "Unknown",
                ProviderName = o.Provider?.Profile?.FullName ?? "Unknown",
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                CreatedAt = o.CreatedAt
            }).ToList();
        }

        public async Task<List<ReportDetailItem>> GetReportDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            // For detail modal, show ALL reports (ignore date range)
            var query = _context.Reports
                .Include(r => r.Reporter)
                .ThenInclude(u => u.Profile)
                .AsQueryable();

            // Filter for pending reports
            if (!string.IsNullOrEmpty(filter) && filter == "pending")
            {
                query = query.Where(r => r.Status == ReportStatus.open);
            }

            // Apply search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Subject.Contains(search) ||
                                        r.Reporter.Profile.FullName.Contains(search));
            }

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            return reports.Select(r => new ReportDetailItem
            {
                Id = r.Id,
                Subject = r.Subject,
                ReporterName = r.Reporter?.Profile?.FullName ?? "Unknown",
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<List<ViolationDetailItem>> GetViolationDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            // For detail modal, show ALL violations (ignore date range)
            var query = _context.RentalViolations
                .Include(v => v.OrderItem)
                .ThenInclude(oi => oi.Order)
                .ThenInclude(o => o.Customer)
                .ThenInclude(c => c.Profile)
                .AsQueryable();

            // Filter for active violations
            if (!string.IsNullOrEmpty(filter) && filter == "active")
            {
                query = query.Where(v => v.Status == ViolationStatus.PENDING || 
                                        v.Status == ViolationStatus.CUSTOMER_REJECTED);
            }

            // Apply search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(v => v.Description.Contains(search) ||
                                        v.OrderItem.Order.Customer.Profile.FullName.Contains(search));
            }

            var violations = await query
                .OrderByDescending(v => v.CreatedAt)
                .Take(50)
                .ToListAsync();

            return violations.Select(v => new ViolationDetailItem
            {
                Id = v.ViolationId,
                Description = v.Description,
                CustomerName = v.OrderItem?.Order?.Customer?.Profile?.FullName ?? "Unknown",
                Status = v.Status.ToString(),
                FineAmount = v.PenaltyAmount,
                CreatedAt = v.CreatedAt
            }).ToList();
        }

        public async Task<List<UserDetailItem>> GetUserDetailsAsync(string? filter, DateTime startDate, DateTime endDate, string? search)
        {
            // For detail modal, show ALL users (ignore date range)
            var query = _context.Users
                .Include(u => u.Profile)
                .AsQueryable();

            // Filter for banned users
            if (!string.IsNullOrEmpty(filter) && filter == "banned")
            {
                query = query.Where(u => u.IsActive == false);
            }

            // Apply search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Profile.FullName.Contains(search) ||
                                        u.Email.Contains(search));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Take(200) // Increase limit for banned users
                .ToListAsync();

            return users.Select(u => new UserDetailItem
            {
                Id = u.Id,
                FullName = u.Profile?.FullName ?? "Unknown",
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive ?? true,
                CreatedAt = u.CreatedAt
            }).ToList();
        }
    }
}

