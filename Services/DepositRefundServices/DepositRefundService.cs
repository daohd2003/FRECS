using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using Repositories.DepositRefundRepositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.DepositRefundServices
{
    public class DepositRefundService : IDepositRefundService
    {
        private readonly IDepositRefundRepository _repository;

        public DepositRefundService(IDepositRefundRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<DepositRefundDto>> GetAllRefundRequestsAsync(TransactionStatus? status = null)
        {
            return await _repository.GetAllRefundRequestsAsync(status);
        }

        public async Task<DepositRefundDetailDto?> GetRefundDetailAsync(Guid refundId)
        {
            return await _repository.GetRefundDetailAsync(refundId);
        }

        public async Task<IEnumerable<DepositRefundDto>> GetCustomerRefundsAsync(Guid customerId)
        {
            return await _repository.GetCustomerRefundsAsync(customerId);
        }

        public async Task<bool> ProcessRefundAsync(Guid refundId, Guid adminId, bool isApproved, Guid? bankAccountId, string? notes, string? externalTransactionId = null)
        {
            if (isApproved)
            {
                return await _repository.ApproveRefundAsync(refundId, adminId, bankAccountId, notes, externalTransactionId);
            }
            else
            {
                return await _repository.RejectRefundAsync(refundId, adminId, notes);
            }
        }

        public async Task<bool> ReopenRefundAsync(Guid refundId)
        {
            return await _repository.ReopenRefundAsync(refundId);
        }

        public async Task<int> GetPendingRefundCountAsync()
        {
            return await _repository.GetPendingRefundCountAsync();
        }
    }
}

