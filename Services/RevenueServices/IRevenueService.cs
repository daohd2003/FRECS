using BusinessObject.DTOs.RevenueDtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.RevenueServices
{
    public interface IRevenueService
    {
        Task<RevenueStatsDto> GetRevenueStatsAsync(Guid userId, string period = "month", DateTime? startDate = null, DateTime? endDate = null);
        Task<PayoutSummaryDto> GetPayoutSummaryAsync(Guid userId);
        Task<List<PayoutHistoryDto>> GetPayoutHistoryAsync(Guid userId, int page = 1, int pageSize = 10);
        Task<List<BankAccountDto>> GetBankAccountsAsync(Guid userId);
        Task<BankAccountDto> CreateBankAccountAsync(Guid userId, CreateBankAccountDto dto);
        Task<bool> UpdateBankAccountAsync(Guid userId, Guid accountId, CreateBankAccountDto dto);
        Task<bool> DeleteBankAccountAsync(Guid userId, Guid accountId);
        Task<bool> SetPrimaryBankAccountAsync(Guid userId, Guid accountId);
        Task<bool> RequestPayoutAsync(Guid userId, decimal amount);
    }
}

