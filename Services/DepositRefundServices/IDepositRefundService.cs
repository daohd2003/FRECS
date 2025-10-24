using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.DepositRefundServices
{
    public interface IDepositRefundService
    {
        Task<IEnumerable<DepositRefundDto>> GetAllRefundRequestsAsync(TransactionStatus? status = null);
        Task<DepositRefundDetailDto?> GetRefundDetailAsync(Guid refundId);
        Task<IEnumerable<DepositRefundDto>> GetCustomerRefundsAsync(Guid customerId);
        Task<bool> ProcessRefundAsync(Guid refundId, Guid adminId, bool isApproved, Guid? bankAccountId, string? notes, string? externalTransactionId = null);
        Task<bool> ReopenRefundAsync(Guid refundId);
        Task<int> GetPendingRefundCountAsync();
    }
}

