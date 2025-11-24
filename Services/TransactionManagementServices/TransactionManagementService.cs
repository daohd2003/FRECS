using BusinessObject.DTOs.TransactionDto;
using Repositories.TransactionRepositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.TransactionManagementServices
{
    public class TransactionManagementService : ITransactionManagementService
    {
        private readonly ITransactionManagementRepository _repository;

        public TransactionManagementService(ITransactionManagementRepository repository)
        {
            _repository = repository;
        }

        public async Task<(List<TransactionManagementDto> Transactions, int TotalCount)> GetAllTransactionsAsync(TransactionFilterDto filter)
        {
            return await _repository.GetAllTransactionsAsync(filter);
        }

        public async Task<TransactionManagementDto> GetTransactionDetailAsync(Guid transactionId)
        {
            return await _repository.GetTransactionDetailAsync(transactionId);
        }

        public async Task<Repositories.TransactionRepositories.TransactionStatisticsDto> GetTransactionStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            return await _repository.GetTransactionStatisticsAsync(startDate, endDate);
        }
    }
}
