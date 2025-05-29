using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ProfileRepositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly ShareItDbContext _context;

        public ProfileRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<Profile?> GetByUserIdAsync(Guid userId)
        {
            return await _context.Profiles
                .Include(p => p.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        public async Task AddAsync(Profile profile)
        {
            await _context.Profiles.AddAsync(profile);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Profile updatedProfile)
        {
            // 1. Lấy entity tracked từ DB
            var existingProfile = await _context.Profiles.FindAsync(updatedProfile.Id);

            if (existingProfile == null)
                throw new Exception("Profile not found");

            // 2. Copy dữ liệu mới vào entity tracked
            _context.Entry(existingProfile).CurrentValues.SetValues(updatedProfile);

            // 3. EF Core sẽ tự đánh dấu các property khác nhau là Modified
            await _context.SaveChangesAsync();
        }
    }
}
