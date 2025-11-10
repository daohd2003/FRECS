using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.WithdrawalRepositories
{
    public interface IWithdrawalRepository : IRepository<WithdrawalRequest>
    {
        /// <summary>
        /// Get all withdrawal requests for a specific provider
        /// </summary>
        Task<IEnumerable<WithdrawalRequest>> GetByProviderIdAsync(Guid providerId);

        /// <summary>
        /// Get withdrawal request by ID with related entities (Provider, BankAccount, ProcessedByAdmin)
        /// </summary>
        Task<WithdrawalRequest?> GetByIdWithDetailsAsync(Guid id);

        /// <summary>
        /// Get all pending withdrawal requests (for admin)
        /// </summary>
        Task<IEnumerable<WithdrawalRequest>> GetPendingRequestsAsync();

        /// <summary>
        /// Get all withdrawal requests (for admin - pending, completed, rejected)
        /// </summary>
        Task<IEnumerable<WithdrawalRequest>> GetAllRequestsAsync();

        /// <summary>
        /// Get total amount of pending withdrawals for a provider
        /// </summary>
        Task<decimal> GetTotalPendingAmountAsync(Guid providerId);

        /// <summary>
        /// Get total amount of completed withdrawals for a provider
        /// </summary>
        Task<decimal> GetTotalCompletedAmountAsync(Guid providerId);
    }
}

