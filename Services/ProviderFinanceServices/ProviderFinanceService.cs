using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using Repositories.TransactionRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProviderFinanceServices
{
    public class ProviderFinanceService : IProviderFinanceService
    {
        private readonly ITransactionRepository _transactionRepo;
        private readonly IBankAccountRepository _bankAccountRepo;

        public ProviderFinanceService(ITransactionRepository transactionRepo, IBankAccountRepository bankAccountRepo)
        {
            _transactionRepo = transactionRepo;
            _bankAccountRepo = bankAccountRepo;
        }

        public async Task<decimal> GetTotalRevenue(Guid providerId)
        {
            return await _transactionRepo.GetTotalReceivedByProviderAsync(providerId);
        }

        public async Task<BankAccount?> GetPrimaryBankAccount(Guid providerId)
        {
            return await _bankAccountRepo.GetPrimaryAccountByUserAsync(providerId);
        }

        public async Task<IEnumerable<TransactionSummaryDto>> GetTransactionDetails(Guid providerId)
        {
            return await _transactionRepo.GetTransactionsByProviderAsync(providerId);
        }

        public async Task<AllProvidersPaymentSummaryDto> GetAllProviderPaymentsSummaryAsync()
        {
            return await _transactionRepo.GetAllProviderPaymentsSummaryAsync();
        }

        public async Task<IEnumerable<ProviderPaymentDto>> GetAllProviderPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            return await _transactionRepo.GetAllProviderPaymentsAsync(startDate, endDate);
        }
    }
}
