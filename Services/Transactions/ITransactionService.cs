using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.DTOs.VNPay.Request;
using BusinessObject.Models;
 
namespace Services.Transactions
{
    public interface ITransactionService
    {
        Task<Transaction> SaveTransactionAsync(Transaction transaction);
        Task<IEnumerable<TransactionSummaryDto>> GetUserTransactionsAsync(Guid userId);
        Task<TransactionSummaryDto?> GetTransactionByIdAsync(Guid transactionId);
        Task<bool> ProcessSepayWebhookAsync(SepayWebhookRequest request);
    }
}
