using BusinessObject.Models;
using BusinessObject.Enums;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.DiscountCodeRepositories
{
    public class DiscountCodeRepository : Repository<DiscountCode>, IDiscountCodeRepository
    {
        public DiscountCodeRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<DiscountCode?> GetByCodeAsync(string code)
        {
            return await _context.DiscountCodes
                .FirstOrDefaultAsync(dc => dc.Code == code);
        }

        public async Task<IEnumerable<DiscountCode>> GetActiveDiscountCodesAsync()
        {
            return await _context.DiscountCodes
                .Where(dc => dc.Status == DiscountStatus.Active && dc.ExpirationDate > DateTime.UtcNow)
                .OrderBy(dc => dc.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DiscountCode>> GetExpiredDiscountCodesAsync()
        {
            return await _context.DiscountCodes
                .Where(dc => dc.ExpirationDate <= DateTime.UtcNow || dc.Status == DiscountStatus.Expired)
                .OrderBy(dc => dc.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
        {
            var query = _context.DiscountCodes.Where(dc => dc.Code == code);
            
            if (excludeId.HasValue)
            {
                query = query.Where(dc => dc.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task UpdateExpiredStatusAsync()
        {
            var expiredCodes = await _context.DiscountCodes
                .Where(dc => dc.Status == DiscountStatus.Active && dc.ExpirationDate <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var code in expiredCodes)
            {
                code.Status = DiscountStatus.Expired;
                code.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredCodes.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<UsedDiscountCode>> GetUsageHistoryAsync(Guid discountCodeId)
        {
            return await _context.UsedDiscountCodes
                .Include(udc => udc.User)
                    .ThenInclude(u => u.Profile)
                .Include(udc => udc.Order)
                .Where(udc => udc.DiscountCodeId == discountCodeId)
                .OrderByDescending(udc => udc.UsedAt)
                .ToListAsync();
        }

        public override async Task<IEnumerable<DiscountCode>> GetAllAsync()
        {
            return await _context.DiscountCodes
                .Include(dc => dc.UsedDiscountCodes)
                .OrderByDescending(dc => dc.CreatedAt)
                .ToListAsync();
        }

        public override async Task<DiscountCode?> GetByIdAsync(Guid id)
        {
            return await _context.DiscountCodes
                .Include(dc => dc.UsedDiscountCodes)
                .FirstOrDefaultAsync(dc => dc.Id == id);
        }
    }
}
