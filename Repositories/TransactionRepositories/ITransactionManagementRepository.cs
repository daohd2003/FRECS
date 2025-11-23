using BusinessObject.DTOs.TransactionDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.TransactionRepositories
{
    /// <summary>
    /// Repository riêng cho Transaction Management
    /// TÁCH BIỆT với ITransactionRepository để không ảnh hưởng đến logic hiện tại
    /// </summary>
    public interface ITransactionManagementRepository
    {
        /// <summary>
        /// Lấy danh sách tất cả giao dịch với filter
        /// </summary>
        Task<(List<TransactionManagementDto> Transactions, int TotalCount)> GetAllTransactionsAsync(TransactionFilterDto filter);

        /// <summary>
        /// Lấy chi tiết một giao dịch
        /// </summary>
        Task<TransactionManagementDto> GetTransactionDetailAsync(Guid transactionId);

        /// <summary>
        /// Lấy thống kê tổng quan
        /// </summary>
        Task<TransactionStatisticsDto> GetTransactionStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

    /// <summary>
    /// DTO cho thống kê giao dịch
    /// </summary>
    public class TransactionStatisticsDto
    {
        public decimal TotalPurchaseAmount { get; set; }
        public decimal TotalRentalAmount { get; set; }
        public decimal TotalDepositRefundAmount { get; set; }
        public decimal TotalProviderWithdrawalAmount { get; set; }
        public decimal TotalPenaltyAmount { get; set; }
        public decimal TotalCompensationAmount { get; set; }
        
        public int TotalTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int FailedTransactions { get; set; }
    }
}
