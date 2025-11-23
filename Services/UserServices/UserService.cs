using AutoMapper;
using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.ReportDto;
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
    }
}
