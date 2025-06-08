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
        Task<IEnumerable<Transaction>> GetTransactionsByProviderAsync(Guid providerId);
        Task<decimal> GetTotalReceivedByProviderAsync(Guid providerId);
    }
}
