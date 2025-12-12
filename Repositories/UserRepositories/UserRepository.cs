using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.UserRepositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ShareItDbContext context) : base(context)
        {
        }

        /// <summary>
        /// Lấy tất cả users với Profile
        /// Dùng AsNoTracking để tối ưu performance (read-only)
        /// </summary>
        /// <returns>Danh sách User entities</returns>
        public override async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Profile) // Eager loading Profile
                .AsNoTracking() // Không track changes (read-only)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tất cả users với Profile và Orders (cả customer và provider orders)
        /// Dùng cho admin dashboard hoặc báo cáo
        /// </summary>
        /// <returns>Danh sách User entities với đầy đủ thông tin orders</returns>
        public async Task<IEnumerable<User>> GetAllWithOrdersAsync()
        {
            return await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.OrdersAsCustomer) // Orders mà user là customer
                    .ThenInclude(o => o.Items) // Include OrderItems
                .Include(u => u.OrdersAsProvider) // Orders mà user là provider
                    .ThenInclude(o => o.Items) // Include OrderItems
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Lấy user theo ID với Orders và OrderItems
        /// Dùng cho load order statistics của 1 user cụ thể
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User entity với orders hoặc null nếu không tồn tại</returns>
        public async Task<User?> GetUserWithOrdersAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.OrdersAsCustomer)
                    .ThenInclude(o => o.Items)
                .Include(u => u.OrdersAsProvider)
                    .ThenInclude(o => o.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        /// <summary>
        /// Lấy user theo ID với Profile
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User entity hoặc null nếu không tồn tại</returns>
        public override async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Profile) // Eager loading Profile
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        /// <summary>
        /// Lấy user theo email với Profile
        /// Dùng cho login, forgot password, email verification
        /// </summary>
        /// <param name="email">Email của user</param>
        /// <returns>User entity hoặc null nếu không tồn tại</returns>
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Profile) // Eager loading Profile
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        /// <summary>
        /// Lấy hoặc tạo user từ Google OAuth
        /// Logic: Kiểm tra email → Kiểm tra GoogleId → Tạo mới nếu chưa tồn tại
        /// </summary>
        /// <param name="payload">Thông tin từ Google (Email, Sub/GoogleId, Name, Picture)</param>
        /// <returns>User entity (existing hoặc newly created), null nếu email đã đăng ký bằng traditional login</returns>
        public async Task<User> GetOrCreateUserAsync(GooglePayload payload)
        {
            // Bước 1: Kiểm tra email đã tồn tại chưa
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == payload.Email);

            if (existingUser != null)
            {
                // Nếu user đã tồn tại nhưng không có GoogleId
                // → Đã đăng ký bằng email/password truyền thống
                // → Không cho phép login bằng Google (tránh conflict)
                if (string.IsNullOrEmpty(existingUser.GoogleId))
                {
                    return null; // Email đã được dùng cho traditional login
                }

                // User đã tồn tại và có GoogleId → Trả về user hiện tại
                return existingUser;
            }

            // Bước 2: Kiểm tra GoogleId (Sub) đã tồn tại chưa
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.GoogleId == payload.Sub);

            if (user == null)
            {
                // Bước 3: Tạo user mới nếu chưa tồn tại
                // Tạo username ngẫu nhiên (user + 6 chữ số)
                string username;
                do
                {
                    username = "user" + new Random().Next(100000, 999999);
                }
                while (await _context.Profiles.AnyAsync(u => u.FullName == username)); // Đảm bảo unique

                // Tạo user mới với thông tin từ Google
                user = new User
                {
                    Email = payload.Email,
                    GoogleId = payload.Sub, // Lưu Google ID để nhận diện sau này
                    Role = UserRole.customer, // Mặc định là customer
                    PasswordHash = "", // Không có password (login bằng Google)
                    RefreshToken = "",
                    RefreshTokenExpiryTime = DateTimeHelper.GetVietnamTime(),
                    IsActive = true,
                    CreatedAt = DateTimeHelper.GetVietnamTime(),
                    EmailConfirmed = true, // Google đã verify email
                    Profile = new Profile
                    {
                        FullName = username, // Username tạm thời
                        ProfilePictureUrl = "https://res.cloudinary.com/dtzg1vs7r/image/upload/v1765160862/t%E1%BA%A3i_xu%E1%BB%91ng_zhflev.jpg" // Avatar mặc định
                    }
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // User đã tồn tại với GoogleId → Cập nhật email nếu có thay đổi
                if (user.Email != payload.Email)
                {
                    user.Email = payload.Email;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            return user;
        }

        public async Task<User> GetOrCreateUserAsync(FacebookPayload payload)
        {
            // If email is provided, try by email first
            if (!string.IsNullOrWhiteSpace(payload.Email))
            {
                var existingByEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                if (existingByEmail != null)
                {
                    return existingByEmail;
                }
            }

            // Try by Facebook Id stored in GoogleId slot? We won't change model, so reuse GoogleId to store social id
            var existingByFbId = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Id);
            if (existingByFbId != null)
            {
                // Update email if newly available
                if (!string.IsNullOrWhiteSpace(payload.Email) && existingByFbId.Email != payload.Email)
                {
                    existingByFbId.Email = payload.Email;
                    _context.Users.Update(existingByFbId);
                    await _context.SaveChangesAsync();
                }
                return existingByFbId;
            }

            // Create new user
            string username;
            do
            {
                username = "user" + new Random().Next(100000, 999999);
            }
            while (await _context.Profiles.AnyAsync(u => u.FullName == username));

            var user = new User
            {
                Email = payload.Email ?? string.Empty,
                GoogleId = payload.Id, // reuse field to store facebook id
                Role = UserRole.customer,
                PasswordHash = string.Empty,
                RefreshToken = string.Empty,
                RefreshTokenExpiryTime = DateTimeHelper.GetVietnamTime(),
                IsActive = true,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                Profile = new Profile
                {
                    FullName = string.IsNullOrWhiteSpace(payload.Name) ? username : payload.Name,
                    ProfilePictureUrl = string.IsNullOrWhiteSpace(payload.PictureUrl)
                        ? "https://res.cloudinary.com/dtzg1vs7r/image/upload/v1765160862/t%E1%BA%A3i_xu%E1%BB%91ng_zhflev.jpg"
                        : payload.PictureUrl
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        {
            return await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        }

        public new async Task<IEnumerable<User>> GetByCondition(Expression<Func<User, bool>> expression)
        {
            return await _context.Users
                .Include(u => u.Profile)
                .Where(expression)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all users with order count only (optimized for list view)
        /// Uses COUNT in database instead of loading all orders
        /// </summary>
        public async Task<IEnumerable<(User User, int OrderCount)>> GetAllUsersWithOrderCountAsync()
        {
            var result = await _context.Users
                .Include(u => u.Profile)
                .Select(u => new
                {
                    User = u,
                    // Count orders based on role
                    OrderCount = u.Role == UserRole.provider
                        ? u.OrdersAsProvider.Count()
                        : u.OrdersAsCustomer.Count()
                })
                .AsNoTracking()
                .ToListAsync();

            return result.Select(x => (x.User, x.OrderCount));
        }

        /// <summary>
        /// Get user order statistics using optimized database queries (COUNT/SUM)
        /// No loading of all orders - everything computed in database
        /// </summary>
        public async Task<UserOrderStatsDto?> GetUserOrderStatsOptimizedAsync(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return null;

            // Determine which orders to query based on role
            var isProvider = user.Role == UserRole.provider;

            // Query orders with status counts - all in database
            var orderStats = isProvider
                ? await _context.Orders
                    .Where(o => o.ProviderId == userId)
                    .GroupBy(o => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Pending = g.Count(o => o.Status == OrderStatus.pending),
                        Approved = g.Count(o => o.Status == OrderStatus.approved),
                        InTransit = g.Count(o => o.Status == OrderStatus.in_transit),
                        InUse = g.Count(o => o.Status == OrderStatus.in_use),
                        Returning = g.Count(o => o.Status == OrderStatus.returning),
                        Returned = g.Count(o => o.Status == OrderStatus.returned),
                        Cancelled = g.Count(o => o.Status == OrderStatus.cancelled),
                        ReturnedWithIssue = g.Count(o => o.Status == OrderStatus.returned_with_issue)
                    })
                    .FirstOrDefaultAsync()
                : await _context.Orders
                    .Where(o => o.CustomerId == userId)
                    .GroupBy(o => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Pending = g.Count(o => o.Status == OrderStatus.pending),
                        Approved = g.Count(o => o.Status == OrderStatus.approved),
                        InTransit = g.Count(o => o.Status == OrderStatus.in_transit),
                        InUse = g.Count(o => o.Status == OrderStatus.in_use),
                        Returning = g.Count(o => o.Status == OrderStatus.returning),
                        Returned = g.Count(o => o.Status == OrderStatus.returned),
                        Cancelled = g.Count(o => o.Status == OrderStatus.cancelled),
                        ReturnedWithIssue = g.Count(o => o.Status == OrderStatus.returned_with_issue)
                    })
                    .FirstOrDefaultAsync();

            // Query orders for earnings/spending calculation
            // For Provider: rental orders need status returned, purchase need status in_use
            // For Customer: all completed orders (in_use, returned, returned_with_issue)
            // All orders must have payment completed (Transaction.Status == completed)
            var completedOrdersQuery = isProvider
                ? _context.Orders
                    .Include(o => o.Items)
                    .Include(o => o.Transactions)
                    .Where(o => o.ProviderId == userId &&
                               o.Transactions.Any(t => t.Status == TransactionStatus.completed) &&
                               (o.Status == OrderStatus.returned ||
                                (o.Status == OrderStatus.in_use && o.Items.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase))))
                : _context.Orders
                    .Include(o => o.Items)
                    .Include(o => o.Transactions)
                    .Where(o => o.CustomerId == userId &&
                               o.Transactions.Any(t => t.Status == TransactionStatus.completed) &&
                               (o.Status == OrderStatus.in_use || 
                                o.Status == OrderStatus.returned || 
                                o.Status == OrderStatus.returned_with_issue));

            var completedOrders = await completedOrdersQuery.AsNoTracking().ToListAsync();

            // Calculate totals from orders
            decimal rentalTotal = 0;
            decimal purchaseTotal = 0;
            int rentalCount = 0;
            int purchaseCount = 0;

            foreach (var order in completedOrders)
            {
                // Calculate actual amount (Subtotal - all discounts, NOT including deposit)
                var totalDiscount = order.DiscountAmount + order.ItemRentalCountDiscount + order.LoyaltyDiscount;
                var actualAmount = order.Subtotal - totalDiscount;

                // For Provider: also deduct commission (platform fee)
                if (isProvider)
                {
                    var totalCommission = order.Items?.Sum(i => i.CommissionAmount) ?? 0;
                    actualAmount -= totalCommission;
                }

                // Determine if rental or purchase based on items
                var hasRentalItems = order.Items?.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental) ?? false;
                var hasPurchaseItems = order.Items?.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase) ?? false;

                // For Provider: check if order should be counted based on status and type
                // - Rental: only count when returned
                // - Purchase: only count when in_use
                bool shouldCount = true;
                if (isProvider)
                {
                    if (hasRentalItems && !hasPurchaseItems)
                    {
                        // Pure rental: only count when returned
                        shouldCount = order.Status == OrderStatus.returned;
                    }
                    else if (hasPurchaseItems && !hasRentalItems)
                    {
                        // Pure purchase: only count when in_use
                        shouldCount = order.Status == OrderStatus.in_use;
                    }
                    else
                    {
                        // Mixed: only count when returned (rental requires return)
                        shouldCount = order.Status == OrderStatus.returned;
                    }
                }

                if (!shouldCount) continue;

                if (hasRentalItems && !hasPurchaseItems)
                {
                    rentalTotal += actualAmount;
                    rentalCount += order.Items?.Count(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental) ?? 0;
                }
                else if (hasPurchaseItems && !hasRentalItems)
                {
                    purchaseTotal += actualAmount;
                    purchaseCount += order.Items?.Count(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase) ?? 0;
                }
                else
                {
                    // Mixed order - split proportionally based on item values
                    var rentalItems = order.Items?.Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental).ToList() ?? new List<OrderItem>();
                    var purchaseItems = order.Items?.Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase).ToList() ?? new List<OrderItem>();
                    
                    var rentalValue = rentalItems.Sum(i => i.DailyRate * (i.RentalDays ?? 0) * i.Quantity);
                    var purchaseValue = purchaseItems.Sum(i => i.DailyRate * i.Quantity);
                    var totalValue = rentalValue + purchaseValue;

                    if (totalValue > 0)
                    {
                        rentalTotal += actualAmount * (rentalValue / totalValue);
                        purchaseTotal += actualAmount * (purchaseValue / totalValue);
                    }
                    
                    rentalCount += rentalItems.Count;
                    purchaseCount += purchaseItems.Count;
                }
            }

            // For Provider: add penalty revenue from violations
            decimal penaltyRevenue = 0;
            if (isProvider)
            {
                penaltyRevenue = await _context.RentalViolations
                    .Include(rv => rv.OrderItem)
                        .ThenInclude(oi => oi.Order)
                    .Where(rv => rv.OrderItem.Order.ProviderId == userId
                        && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED 
                            || rv.Status == ViolationStatus.RESOLVED 
                            || rv.Status == ViolationStatus.RESOLVED_BY_ADMIN))
                    .SumAsync(rv => rv.PenaltyAmount);
            }

            // Calculate total earnings (for provider: include penalty revenue)
            var totalEarnings = rentalTotal + purchaseTotal + penaltyRevenue;

            return new UserOrderStatsDto
            {
                TotalOrders = orderStats?.Total ?? 0,
                OrdersByStatus = new OrdersByStatusDto
                {
                    Pending = orderStats?.Pending ?? 0,
                    Approved = orderStats?.Approved ?? 0,
                    InTransit = orderStats?.InTransit ?? 0,
                    InUse = orderStats?.InUse ?? 0,
                    Returning = orderStats?.Returning ?? 0,
                    Returned = orderStats?.Returned ?? 0,
                    Cancelled = orderStats?.Cancelled ?? 0,
                    ReturnedWithIssue = orderStats?.ReturnedWithIssue ?? 0
                },
                ReturnedOrdersBreakdown = new ReturnedOrdersBreakdownDto
                {
                    RentalProductsCount = rentalCount,
                    RentalTotalEarnings = rentalTotal,
                    PurchaseProductsCount = purchaseCount,
                    PurchaseTotalEarnings = purchaseTotal,
                    TotalEarnings = totalEarnings,
                    RentalOrdersCount = rentalCount,
                    PurchaseOrdersCount = purchaseCount
                }
            };
        }
    }
}
