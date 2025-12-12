using AutoMapper;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Models;
using BusinessObject.Enums;
using BusinessObject.Utilities;
using Repositories.DiscountCodeRepositories;

namespace Services.DiscountCodeServices
{
    public class DiscountCodeService : IDiscountCodeService
    {
        private readonly IDiscountCodeRepository _discountCodeRepository;
        private readonly IMapper _mapper;

        public DiscountCodeService(IDiscountCodeRepository discountCodeRepository, IMapper mapper)
        {
            _discountCodeRepository = discountCodeRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<DiscountCodeDto>> GetAllDiscountCodesAsync()
        {
            // Auto-update expired status before returning
            await UpdateExpiredStatusAsync();
            
            var discountCodes = await _discountCodeRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<DiscountCodeDto>>(discountCodes);
        }

        public async Task<DiscountCodeDto?> GetDiscountCodeByIdAsync(Guid id)
        {
            var discountCode = await _discountCodeRepository.GetByIdAsync(id);
            return discountCode != null ? _mapper.Map<DiscountCodeDto>(discountCode) : null;
        }

        public async Task<DiscountCodeDto?> GetDiscountCodeByCodeAsync(string code)
        {
            var discountCode = await _discountCodeRepository.GetByCodeAsync(code);
            return discountCode != null ? _mapper.Map<DiscountCodeDto>(discountCode) : null;
        }

        public async Task<DiscountCodeDto> CreateDiscountCodeAsync(CreateDiscountCodeDto createDto)
        {
            // Check if code is unique
            if (!await IsCodeUniqueAsync(createDto.Code))
            {
                throw new InvalidOperationException($"Discount code '{createDto.Code}' already exists.");
            }

            // Validate expiration date (compare with Vietnam time UTC+7)
            if (createDto.ExpirationDate <= DateTimeHelper.GetVietnamTime())
            {
                throw new InvalidOperationException("Expiration date must be in the future.");
            }

            var discountCode = _mapper.Map<DiscountCode>(createDto);
            await _discountCodeRepository.AddAsync(discountCode);
            
            return _mapper.Map<DiscountCodeDto>(discountCode);
        }

        public async Task<DiscountCodeDto?> UpdateDiscountCodeAsync(Guid id, UpdateDiscountCodeDto updateDto)
        {
            var existingDiscountCode = await _discountCodeRepository.GetByIdAsync(id);
            if (existingDiscountCode == null)
            {
                return null;
            }

            // Check if code is unique (excluding current record)
            if (!await IsCodeUniqueAsync(updateDto.Code, id))
            {
                throw new InvalidOperationException($"Discount code '{updateDto.Code}' already exists.");
            }

            // Validate expiration date (compare with Vietnam time UTC+7)
            if (updateDto.ExpirationDate <= DateTimeHelper.GetVietnamTime() && updateDto.Status == DiscountStatus.Active)
            {
                throw new InvalidOperationException("Cannot set Active status with past expiration date.");
            }

            // Map updates to existing entity
            _mapper.Map(updateDto, existingDiscountCode);
            
            await _discountCodeRepository.UpdateAsync(existingDiscountCode);
            
            return _mapper.Map<DiscountCodeDto>(existingDiscountCode);
        }

        public async Task<bool> DeleteDiscountCodeAsync(Guid id)
        {
            var discountCode = await _discountCodeRepository.GetByIdAsync(id);
            if (discountCode == null)
            {
                return false;
            }

            // Check if discount code has been used
            if (discountCode.UsedCount > 0)
            {
                throw new InvalidOperationException("Cannot delete a discount code that has been used.");
            }

            return await _discountCodeRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<DiscountCodeDto>> GetActiveDiscountCodesAsync()
        {
            await UpdateExpiredStatusAsync();
            var activeDiscountCodes = await _discountCodeRepository.GetActiveDiscountCodesAsync();
            return _mapper.Map<IEnumerable<DiscountCodeDto>>(activeDiscountCodes);
        }

        public async Task<IEnumerable<DiscountCodeDto>> GetExpiredDiscountCodesAsync()
        {
            var expiredDiscountCodes = await _discountCodeRepository.GetExpiredDiscountCodesAsync();
            return _mapper.Map<IEnumerable<DiscountCodeDto>>(expiredDiscountCodes);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
        {
            return await _discountCodeRepository.IsCodeUniqueAsync(code, excludeId);
        }

        public async Task UpdateExpiredStatusAsync()
        {
            await _discountCodeRepository.UpdateExpiredStatusAsync();
        }

        public async Task<IEnumerable<UsedDiscountCodeDto>> GetUsageHistoryAsync(Guid discountCodeId)
        {
            var usageHistory = await _discountCodeRepository.GetUsageHistoryAsync(discountCodeId);
            return _mapper.Map<IEnumerable<UsedDiscountCodeDto>>(usageHistory);
        }

        public async Task<List<Guid>> GetUsedDiscountCodeIdsByUserAsync(Guid userId)
        {
            return await _discountCodeRepository.GetUsedDiscountCodeIdsByUserAsync(userId);
        }

        public async Task RecordDiscountCodeUsageAsync(Guid userId, Guid discountCodeId, Guid orderId)
        {
            await _discountCodeRepository.RecordDiscountCodeUsageAsync(userId, discountCodeId, orderId);
        }
    }
}
