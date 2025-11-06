using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.TransactionRepositories
{
    public interface ITransactionRepository
    {
        Task<IEnumerable<TransactionSummaryDto>> GetTransactionsByProviderAsync(Guid providerId);
        Task<decimal> GetTotalReceivedByProviderAsync(Guid providerId);
        Task<IEnumerable<ProviderPaymentDto>> GetAllProviderPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<AllProvidersPaymentSummaryDto> GetAllProviderPaymentsSummaryAsync();
        Task<decimal> GetTotalPayoutsByUserAsync(Guid userId);
        Task<List<Transaction>> GetPayoutHistoryAsync(Guid userId, int page, int pageSize);
        Task<List<Transaction>> GetRecentPayoutsAsync(Guid userId, int count);
        Task AddTransactionAsync(Transaction transaction);
    }
}
