using AutoMapper;
using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.UserRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.UserServices
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public UserService(IUserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _userRepository.GetAllAsync();
        }

        public async Task<IEnumerable<User>> GetAllWithOrdersAsync()
        {
            return await _userRepository.GetAllWithOrdersAsync();
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task AddAsync(User user)
        {
            await _userRepository.AddAsync(user);
        }

        public async Task<bool> UpdateAsync(User user)
        {
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            return await _userRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Lấy hoặc tạo user từ Google OAuth
        /// Nếu email đã tồn tại → Trả về user hiện tại
        /// Nếu chưa tồn tại → Tạo user mới với thông tin từ Google
        /// </summary>
        /// <param name="payload">Thông tin từ Google (email, name, picture)</param>
        /// <returns>User entity</returns>
        public async Task<User> GetOrCreateUserAsync(GooglePayload payload)
        {
            return await _userRepository.GetOrCreateUserAsync(payload);
        }

        /// <summary>
        /// Lấy hoặc tạo user từ Facebook OAuth
        /// Nếu email đã tồn tại → Trả về user hiện tại
        /// Nếu chưa tồn tại → Tạo user mới với thông tin từ Facebook
        /// </summary>
        /// <param name="payload">Thông tin từ Facebook (email, name, picture)</param>
        /// <returns>User entity</returns>
        public async Task<User> GetOrCreateUserAsync(FacebookPayload payload)
        {
            return await _userRepository.GetOrCreateUserAsync(payload);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }
        public async Task<IEnumerable<AdminViewModel>> GetAllAdminsAsync()
        {
            // Lấy tất cả user có vai trò là admin từ repository
            var admins = await _userRepository.GetByCondition(u => u.Role == UserRole.admin);

            // Dùng AutoMapper để chuyển đổi sang ViewModel
            return _mapper.Map<IEnumerable<AdminViewModel>>(admins);
        }

        /// <summary>
        /// Khóa tài khoản user (Admin chức năng)
        /// User bị khóa không thể đăng nhập và sử dụng hệ thống
        /// </summary>
        /// <param name="id">ID user cần khóa</param>
        /// <returns>true nếu khóa thành công, false nếu user không tồn tại</returns>
        public async Task<bool> BlockUserAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            user.IsActive = false; // Đặt trạng thái không hoạt động
            return await _userRepository.UpdateAsync(user);
        }

        /// <summary>
        /// Mở khóa tài khoản user (Admin chức năng)
        /// User được mở khóa có thể đăng nhập và sử dụng hệ thống trở lại
        /// </summary>
        /// <param name="id">ID user cần mở khóa</param>
        /// <returns>true nếu mở khóa thành công, false nếu user không tồn tại</returns>
        public async Task<bool> UnblockUserAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            user.IsActive = true; // Đặt trạng thái hoạt động
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<IEnumerable<User>> GetCustomersAndProvidersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Where(u => u.Role == UserRole.customer || u.Role == UserRole.provider);
        }

        public async Task<UserOrderStatsDto?> GetUserOrderStatsAsync(Guid userId)
        {
            var user = await _userRepository.GetUserWithOrdersAsync(userId);
            
            if (user == null)
            {
                return null;
            }
            
            // Get relevant orders based on role
            var ordersAsCustomer = user.OrdersAsCustomer ?? new List<Order>();
            var ordersAsProvider = user.OrdersAsProvider ?? new List<Order>();
            
            var relevantOrders = user.Role == UserRole.provider 
                ? ordersAsProvider 
                : ordersAsCustomer;

            // Count orders by status
            var ordersByStatus = new OrdersByStatusDto
            {
                Pending = relevantOrders.Count(o => o.Status == OrderStatus.pending),
                Approved = relevantOrders.Count(o => o.Status == OrderStatus.approved),
                InTransit = relevantOrders.Count(o => o.Status == OrderStatus.in_transit),
                InUse = relevantOrders.Count(o => o.Status == OrderStatus.in_use),
                Returning = relevantOrders.Count(o => o.Status == OrderStatus.returning),
                Returned = relevantOrders.Count(o => o.Status == OrderStatus.returned),
                Cancelled = relevantOrders.Count(o => o.Status == OrderStatus.cancelled),
                ReturnedWithIssue = relevantOrders.Count(o => o.Status == OrderStatus.returned_with_issue)
            };

            // Calculate earnings/spending for returned orders
            var returnedOrders = relevantOrders
                .Where(o => o.Status == OrderStatus.returned || o.Status == OrderStatus.returned_with_issue)
                .ToList();

            var allReturnedItems = returnedOrders
                .Where(o => o.Items != null)
                .SelectMany(o => o.Items)
                .ToList();

            var rentalProducts = allReturnedItems
                .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental)
                .ToList();
            
            var purchaseProducts = allReturnedItems
                .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                .ToList();

            var rentalEarnings = rentalProducts.Sum(item => 
                item.DailyRate * (item.RentalDays ?? 0) * item.Quantity);

            var purchaseEarnings = purchaseProducts.Sum(item => 
                item.DailyRate * item.Quantity);

            return new UserOrderStatsDto
            {
                TotalOrders = relevantOrders.Count(),
                OrdersByStatus = ordersByStatus,
                ReturnedOrdersBreakdown = new ReturnedOrdersBreakdownDto
                {
                    RentalProductsCount = rentalProducts.Count,
                    RentalTotalEarnings = rentalEarnings,
                    PurchaseProductsCount = purchaseProducts.Count,
                    PurchaseTotalEarnings = purchaseEarnings,
                    TotalEarnings = rentalEarnings + purchaseEarnings,
                    RentalOrdersCount = rentalProducts.Count,
                    PurchaseOrdersCount = purchaseProducts.Count
                }
            };
        }
    }
}
