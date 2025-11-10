using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.DepositRefundRepositories
{
    public interface IDepositRefundRepository
    {
        /// <summary>
        /// Lấy danh sách refund requests cho admin (có filter theo status)
        /// </summary>
        Task<IEnumerable<DepositRefundDto>> GetAllRefundRequestsAsync(TransactionStatus? status = null);
        
        /// <summary>
        /// Lấy chi tiết một refund request
        /// </summary>
        Task<DepositRefundDetailDto?> GetRefundDetailAsync(Guid refundId);
        
        /// <summary>
        /// Lấy refund requests của một customer
        /// </summary>
        Task<IEnumerable<DepositRefundDto>> GetCustomerRefundsAsync(Guid customerId);
        
        /// <summary>
        /// Tạo refund request mới (tự động khi customer trả đồ)
        /// </summary>
        Task<DepositRefund> CreateRefundRequestAsync(DepositRefund refund);
        
        /// <summary>
        /// Admin approve refund
        /// </summary>
        Task<bool> ApproveRefundAsync(Guid refundId, Guid adminId, Guid? bankAccountId, string? notes, string? externalTransactionId = null);
        
        /// <summary>
        /// Admin reject refund
        /// </summary>
        Task<bool> RejectRefundAsync(Guid refundId, Guid adminId, string? notes);
        
        /// <summary>
        /// Reopen a rejected refund request
        /// </summary>
        Task<bool> ReopenRefundAsync(Guid refundId);
        
        /// <summary>
        /// Đếm số lượng pending refunds
        /// </summary>
        Task<int> GetPendingRefundCountAsync();
    }
}

