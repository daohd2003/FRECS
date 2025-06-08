using BusinessObject.DTOs.Login;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.UserRepositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> GetOrCreateUserAsync(GooglePayload payload)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == payload.Email);

            if (existingUser != null)
            {
                if (string.IsNullOrEmpty(existingUser.GoogleId))
                {
                    // Đã đăng ký bằng tài khoản truyền thống
                    return null;
                }

                return existingUser;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.GoogleId == payload.Sub);

            if (user == null)
            {
                string username;
                do
                {
                    // VD: "user" + 6 chữ số random
                    username = "user" + new Random().Next(100000, 999999);
                }
                while (await _context.Profiles.AnyAsync(u => u.FullName == username));

                user = new User
                {
                    Email = payload.Email,
                    GoogleId = payload.Sub,
                    Role = UserRole.customer,
                    PasswordHash = "",
                    RefreshToken = "",
                    RefreshTokenExpiryTime = DateTime.Now,
                    IsActive = true,
                    Profile = new Profile
                    {
                        FullName = username,
                        ProfilePictureUrl = "https://static-00.iconduck.com/assets.00/avatar-default-symbolic-icon-479x512-n8sg74wg.png"
                    }
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật email nếu có thay đổi
                if (user.Email != payload.Email)
                {
                    user.Email = payload.Email;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            return user;
        }
    }
}
