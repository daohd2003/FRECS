using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.ProviderApplicationRepositories
{
    public class ProviderApplicationRepository : Repository<ProviderApplication>, IProviderApplicationRepository
    {
        public ProviderApplicationRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<ProviderApplication?> GetPendingByUserIdAsync(Guid userId)
        {
            return await _context.ProviderApplications
                .Where(a => a.UserId == userId && a.Status == ProviderApplicationStatus.pending)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<ProviderApplication>> GetByStatusAsync(ProviderApplicationStatus status)
        {
            return await _context.ProviderApplications
                .Include(a => a.User)
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
    }
}


