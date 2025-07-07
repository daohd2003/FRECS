using BusinessObject.DTOs.BankAccounts;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProviderFinanceServices
{
    public interface IProviderFinanceService
    {
        Task<decimal> GetTotalRevenue(Guid providerId);
        Task<BankAccount?> GetPrimaryBankAccount(Guid providerId);
        Task<IEnumerable<TransactionSummaryDto>> GetTransactionDetails(Guid providerId);
    }
}
