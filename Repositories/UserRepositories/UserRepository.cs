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
                        ProfilePictureUrl = "https://inkythuatso.com/uploads/thumbnails/800/2023/03/3-anh-dai-dien-trang-inkythuatso-03-15-25-56.jpg" // Avatar mặc định
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
