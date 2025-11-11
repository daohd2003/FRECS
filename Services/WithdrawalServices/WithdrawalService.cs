using BusinessObject.DTOs.WithdrawalDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using Repositories.RevenueRepositories;
using Repositories.WithdrawalRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services.WithdrawalServices
{
    public class WithdrawalService : IWithdrawalService
    {
        private readonly IWithdrawalRepository _withdrawalRepo;
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IRevenueRepository _revenueRepo;

        public WithdrawalService(
            IWithdrawalRepository withdrawalRepo,
            IBankAccountRepository bankAccountRepo,
            IRevenueRepository revenueRepo)
        {
            _withdrawalRepo = withdrawalRepo;
            _bankAccountRepo = bankAccountRepo;
            _revenueRepo = revenueRepo;
        }

        /// <summary>
        /// Get current Vietnam time (UTC+7)
        /// </summary>
        private DateTime GetVietnamTime()
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
        }

        public async Task<WithdrawalResponseDto> RequestPayoutAsync(Guid providerId, WithdrawalRequestDto dto)
        {
            // Validate amount is provided (not null/default)
            if (dto.Amount == 0)
            {
                throw new InvalidOperationException("Please enter a withdrawal amount.");
            }

            // Validate amount is positive
            if (dto.Amount < 0)
            {
                throw new InvalidOperationException("Withdrawal amount must be a positive number.");
            }

            // Validate minimum amount
            const decimal minAmount = 50000;
            if (dto.Amount < minAmount)
            {
                throw new InvalidOperationException($"Withdrawal amount must be greater than {minAmount:N0} VND.");
            }

            // Validate bank account belongs to provider
            var bankAccount = await _bankAccountRepo.GetByIdAndUserAsync(dto.BankAccountId, providerId);
            if (bankAccount == null)
            {
                throw new InvalidOperationException("Bank account not found or does not belong to you.");
            }

            // Check available balance
            var availableBalance = await GetAvailableBalanceAsync(providerId);
            if (dto.Amount > availableBalance)
            {
                throw new InvalidOperationException($"Insufficient balance. Available: {availableBalance:N0} VND");
            }

            // Create withdrawal request
            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                BankAccountId = dto.BankAccountId,
                Amount = dto.Amount,
                Status = WithdrawalStatus.Initiated,
                RequestDate = GetVietnamTime(),
                Notes = dto.Notes
            };

            await _withdrawalRepo.AddAsync(withdrawal);

            // Reload with details for response
            var created = await _withdrawalRepo.GetByIdWithDetailsAsync(withdrawal.Id);
            return MapToResponseDto(created!);
        }

        public async Task<IEnumerable<WithdrawalHistoryDto>> GetWithdrawalHistoryAsync(Guid providerId)
        {
            var withdrawals = await _withdrawalRepo.GetByProviderIdAsync(providerId);
            return withdrawals.Select(w => new WithdrawalHistoryDto
            {
                Id = w.Id,
                Amount = w.Amount,
                Status = w.Status.ToString(),
                RequestDate = w.RequestDate,
                ProcessedAt = w.ProcessedAt,
                BankName = w.BankAccount.BankName,
                AccountLast4 = w.BankAccount.AccountNumber.Length >= 4 
                    ? w.BankAccount.AccountNumber.Substring(w.BankAccount.AccountNumber.Length - 4) 
                    : w.BankAccount.AccountNumber,
                Notes = w.Notes,
                RejectionReason = w.RejectionReason
            });
        }

        public async Task<WithdrawalResponseDto?> GetWithdrawalByIdAsync(Guid id)
        {
            var withdrawal = await _withdrawalRepo.GetByIdWithDetailsAsync(id);
            return withdrawal == null ? null : MapToResponseDto(withdrawal);
        }

        public async Task<IEnumerable<WithdrawalResponseDto>> GetPendingRequestsAsync()
        {
            var requests = await _withdrawalRepo.GetPendingRequestsAsync();
            return requests.Select(MapToResponseDto);
        }

        public async Task<IEnumerable<WithdrawalResponseDto>> GetAllRequestsAsync()
        {
            var requests = await _withdrawalRepo.GetAllRequestsAsync();
            return requests.Select(MapToResponseDto);
        }

        public async Task<WithdrawalResponseDto> ProcessWithdrawalAsync(Guid adminId, ProcessWithdrawalRequestDto dto)
        {
            var withdrawal = await _withdrawalRepo.GetByIdAsync(dto.WithdrawalRequestId);
            if (withdrawal == null)
            {
                throw new InvalidOperationException("Withdrawal request not found.");
            }

            if (withdrawal.Status != WithdrawalStatus.Initiated)
            {
                throw new InvalidOperationException("Only pending requests can be processed.");
            }

            // Validate status
            if (dto.Status != "Completed" && dto.Status != "Rejected")
            {
                throw new ArgumentException("Status must be either 'Completed' or 'Rejected'.");
            }

            // Update withdrawal status
            withdrawal.Status = dto.Status == "Completed" ? WithdrawalStatus.Completed : WithdrawalStatus.Rejected;
            withdrawal.ProcessedAt = GetVietnamTime();
            withdrawal.ProcessedByAdminId = adminId;
            withdrawal.RejectionReason = dto.RejectionReason;
            withdrawal.ExternalTransactionId = dto.ExternalTransactionId;
            withdrawal.AdminNotes = dto.AdminNotes;

            await _withdrawalRepo.UpdateAsync(withdrawal);

            // Reload with details
            var updated = await _withdrawalRepo.GetByIdWithDetailsAsync(withdrawal.Id);
            return MapToResponseDto(updated!);
        }

        public async Task<decimal> GetAvailableBalanceAsync(Guid providerId)
        {
            // Total earnings from orders
            var totalEarnings = await _revenueRepo.GetTotalEarningsAsync(providerId);
            
            // Total completed withdrawals (từ bảng WithdrawalRequests với status Completed)
            var totalCompletedWithdrawals = await _withdrawalRepo.GetTotalCompletedAmountAsync(providerId);
            
            // Pending withdrawal amount (not yet processed)
            var pendingWithdrawals = await _withdrawalRepo.GetTotalPendingAmountAsync(providerId);

            // Available = Earnings - (Completed Withdrawals + Pending Withdrawals)
            return totalEarnings - totalCompletedWithdrawals - pendingWithdrawals;
        }

        private WithdrawalResponseDto MapToResponseDto(WithdrawalRequest withdrawal)
        {
            return new WithdrawalResponseDto
            {
                Id = withdrawal.Id,
                ProviderId = withdrawal.ProviderId,
                ProviderName = withdrawal.Provider?.Profile?.FullName ?? "Unknown",
                ProviderEmail = withdrawal.Provider?.Email,
                BankAccountId = withdrawal.BankAccountId,
                BankName = withdrawal.BankAccount.BankName,
                AccountNumber = withdrawal.BankAccount.AccountNumber,
                AccountHolderName = withdrawal.BankAccount.AccountHolderName,
                RoutingNumber = withdrawal.BankAccount.RoutingNumber,
                Amount = withdrawal.Amount,
                Status = withdrawal.Status.ToString(),
                RequestDate = withdrawal.RequestDate,
                Notes = withdrawal.Notes,
                ProcessedAt = withdrawal.ProcessedAt,
                ProcessedByAdminName = withdrawal.ProcessedByAdmin?.Profile?.FullName,
                RejectionReason = withdrawal.RejectionReason,
                ExternalTransactionId = withdrawal.ExternalTransactionId,
                AdminNotes = withdrawal.AdminNotes
            };
        }
    }
}

