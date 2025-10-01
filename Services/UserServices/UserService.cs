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

        public async Task<User> GetOrCreateUserAsync(GooglePayload payload)
        {
            return await _userRepository.GetOrCreateUserAsync(payload);
        }

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

        public async Task<bool> BlockUserAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            user.IsActive = false;
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<bool> UnblockUserAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            user.IsActive = true;
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<IEnumerable<User>> GetCustomersAndProvidersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Where(u => u.Role == UserRole.customer || u.Role == UserRole.provider);
        }
    }
}
