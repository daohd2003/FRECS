using BusinessObject.DTOs.Login;
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

        public override async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Profile)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetAllWithOrdersAsync()
        {
            return await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.OrdersAsCustomer)
                .Include(u => u.OrdersAsProvider)
                .AsNoTracking()
                .ToListAsync();
        }

        public override async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Profile)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Profile)
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
                    CreatedAt = DateTimeHelper.GetVietnamTime(),
                    Profile = new Profile
                    {
                        FullName = username,
                        ProfilePictureUrl = "https://inkythuatso.com/uploads/thumbnails/800/2023/03/3-anh-dai-dien-trang-inkythuatso-03-15-25-56.jpg"
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
                RefreshTokenExpiryTime = DateTime.Now,
                IsActive = true,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                Profile = new Profile
                {
                    FullName = string.IsNullOrWhiteSpace(payload.Name) ? username : payload.Name,
                    ProfilePictureUrl = string.IsNullOrWhiteSpace(payload.PictureUrl)
                        ? "https://inkythuatso.com/uploads/thumbnails/800/2023/03/3-anh-dai-dien-trang-inkythuatso-03-15-25-56.jpg"
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
    }
}
