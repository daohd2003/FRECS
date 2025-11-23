using BusinessObject.DTOs.TransactionDto;
using Repositories.TransactionRepositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.TransactionManagementServices
{
    /// <summary>
    /// Service riêng cho Transaction Management
    /// TÁCH BIỆT với TransactionService để không ảnh hưởng đến logic hiện tại
    /// </summary>
    public interface ITransactionManagementService
    {
        Task<(List<TransactionManagementDto> Transactions, int TotalCount)> GetAllTransactionsAsync(TransactionFilterDto filter);
        Task<TransactionManagementDto> GetTransactionDetailAsync(Guid transactionId);
        Task<Repositories.TransactionRepositories.TransactionStatisticsDto> GetTransactionStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }
}
