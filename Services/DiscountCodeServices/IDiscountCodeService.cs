using BusinessObject.DTOs.DiscountCodeDto;

namespace Services.DiscountCodeServices
{
    public interface IDiscountCodeService
    {
        Task<IEnumerable<DiscountCodeDto>> GetAllDiscountCodesAsync();
        Task<DiscountCodeDto?> GetDiscountCodeByIdAsync(Guid id);
        Task<DiscountCodeDto?> GetDiscountCodeByCodeAsync(string code);
        Task<DiscountCodeDto> CreateDiscountCodeAsync(CreateDiscountCodeDto createDto);
        Task<DiscountCodeDto?> UpdateDiscountCodeAsync(Guid id, UpdateDiscountCodeDto updateDto);
        Task<bool> DeleteDiscountCodeAsync(Guid id);
        Task<IEnumerable<DiscountCodeDto>> GetActiveDiscountCodesAsync();
        Task<IEnumerable<DiscountCodeDto>> GetExpiredDiscountCodesAsync();
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
        Task UpdateExpiredStatusAsync();
        Task<IEnumerable<UsedDiscountCodeDto>> GetUsageHistoryAsync(Guid discountCodeId);
        Task<List<Guid>> GetUsedDiscountCodeIdsByUserAsync(Guid userId);
        Task RecordDiscountCodeUsageAsync(Guid userId, Guid discountCodeId, Guid orderId);
    }
}
