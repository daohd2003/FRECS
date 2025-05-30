using BusinessObject.DTOs.VNPay.Request;
using BusinessObject.Models;
 
namespace Services.Transactions
{
    public interface ITransactionService
    {
        Task<Transaction> SaveTransactionAsync(Transaction transaction);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId);
        Task<Transaction?> GetTransactionByIdAsync(Guid transactionId);
        Task<bool> ProcessSepayWebhookAsync(SepayWebhookRequest request);
    }
}
