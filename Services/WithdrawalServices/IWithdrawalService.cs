using BusinessObject.DTOs.WithdrawalDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.WithdrawalServices
{
    public interface IWithdrawalService
    {
        /// <summary>
        /// Provider creates a new withdrawal request
        /// </summary>
        Task<WithdrawalResponseDto> RequestPayoutAsync(Guid providerId, WithdrawalRequestDto dto);

        /// <summary>
        /// Get withdrawal history for a provider
        /// </summary>
        Task<IEnumerable<WithdrawalHistoryDto>> GetWithdrawalHistoryAsync(Guid providerId);

        /// <summary>
        /// Get a specific withdrawal request detail
        /// </summary>
        Task<WithdrawalResponseDto?> GetWithdrawalByIdAsync(Guid id);

        /// <summary>
        /// Admin gets all pending withdrawal requests
        /// </summary>
        Task<IEnumerable<WithdrawalResponseDto>> GetPendingRequestsAsync();

        /// <summary>
        /// Admin gets all withdrawal requests (pending, completed, rejected)
        /// </summary>
        Task<IEnumerable<WithdrawalResponseDto>> GetAllRequestsAsync();

        /// <summary>
        /// Admin processes (approve/reject) a withdrawal request
        /// </summary>
        Task<WithdrawalResponseDto> ProcessWithdrawalAsync(Guid adminId, ProcessWithdrawalRequestDto dto);

        /// <summary>
        /// Get provider's available balance for withdrawal
        /// </summary>
        Task<decimal> GetAvailableBalanceAsync(Guid providerId);
    }
}

