using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.DiscountCodeRepositories
{
    public interface IDiscountCodeRepository : IRepository<DiscountCode>
    {
        Task<DiscountCode?> GetByCodeAsync(string code);
        Task<IEnumerable<DiscountCode>> GetActiveDiscountCodesAsync();
        Task<IEnumerable<DiscountCode>> GetExpiredDiscountCodesAsync();
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
        Task UpdateExpiredStatusAsync();
        Task<IEnumerable<UsedDiscountCode>> GetUsageHistoryAsync(Guid discountCodeId);
    }
}
