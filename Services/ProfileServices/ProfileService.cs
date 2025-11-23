using BusinessObject.Models;
using Repositories.ProfileRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProfileServices
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;

        public ProfileService(IProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        /// <summary>
        /// Lấy profile của user theo UserId
        /// Profile chứa thông tin cá nhân: FullName, Phone, Address, ProfilePictureUrl
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <returns>Profile entity hoặc null nếu không tồn tại</returns>
        public async Task<Profile?> GetByUserIdAsync(Guid userId)
        {
            return await _profileRepository.GetByUserIdAsync(userId);
        }

        /// <summary>
        /// Tạo profile mới cho user
        /// Thường được gọi khi user đăng ký tài khoản
        /// </summary>
        /// <param name="profile">Profile entity cần tạo</param>
        public async Task AddAsync(Profile profile)
        {
            await _profileRepository.AddAsync(profile);
        }

        /// <summary>
        /// Cập nhật thông tin profile của user
        /// User có thể cập nhật: FullName, Phone, Address, ProfilePictureUrl
        /// </summary>
        /// <param name="profile">Profile entity đã được cập nhật</param>
        public async Task UpdateAsync(Profile profile)
        {
            await _profileRepository.UpdateAsync(profile);
        }
    }
}
