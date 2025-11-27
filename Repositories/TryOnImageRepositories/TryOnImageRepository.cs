using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Repositories.RepositoryBase;

namespace Repositories.TryOnImageRepositories
{
    public class TryOnImageRepository : Repository<TryOnImage>, ITryOnImageRepository
    {
        private readonly ILogger<TryOnImageRepository>? _logger;

        public TryOnImageRepository(ShareItDbContext context, ILogger<TryOnImageRepository>? logger = null) : base(context)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<TryOnImage>> GetByCustomerIdAsync(Guid customerId, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.TryOnImages
                .Where(t => t.CustomerId == customerId && !t.IsDeleted)
                .Include(t => t.Product)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountByCustomerIdAsync(Guid customerId)
        {
            return await _context.TryOnImages
                .CountAsync(t => t.CustomerId == customerId && !t.IsDeleted);
        }

        public async Task<IEnumerable<TryOnImage>> GetExpiredImagesAsync(int batchSize = 100)
        {
            return await _context.TryOnImages
                .Where(t => t.ExpiresAt <= DateTime.UtcNow && !t.IsDeleted)
                .OrderBy(t => t.ExpiresAt)
                .Take(batchSize)
                .ToListAsync();
        }

        public async Task MarkAsDeletedAsync(Guid id)
        {
            try
            {
                _logger?.LogInformation("Marking Try-On image {ImageId} as deleted in database", id);
                
                // Tìm entity với tracking
                var image = await _context.TryOnImages.FindAsync(id);
                
                if (image == null)
                {
                    _logger?.LogWarning("Try-On image {ImageId} not found in database", id);
                    return;
                }
                
                // Update property
                image.IsDeleted = true;
                
                // Save changes
                var affectedRows = await _context.SaveChangesAsync();
                
                _logger?.LogInformation("Successfully marked Try-On image {ImageId} as deleted. Affected rows: {AffectedRows}", id, affectedRows);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error marking Try-On image {ImageId} as deleted", id);
                throw;
            }
        }

        public async Task MarkAsDeletedBatchAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.ToList();
            await _context.TryOnImages
                .Where(t => idList.Contains(t.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDeleted, true));
        }

        public async Task<int> PurgeDeletedRecordsAsync(int olderThanDays = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            return await _context.TryOnImages
                .Where(t => t.IsDeleted && t.ExpiresAt < cutoffDate)
                .ExecuteDeleteAsync();
        }

        public async Task HardDeleteBatchAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.ToList();
            if (!idList.Any()) return;

            await _context.TryOnImages
                .Where(t => idList.Contains(t.Id))
                .ExecuteDeleteAsync();
            
            _logger?.LogInformation("Hard deleted {Count} Try-On images from database", idList.Count);
        }
    }
}
