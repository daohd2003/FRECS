using AutoMapper;
using BusinessObject.DTOs.IssueResolutionDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.CompensationDisputeRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Services.CompensationDisputeServices
{
    public class CompensationDisputeService : ICompensationDisputeService
    {
        private readonly ICompensationDisputeRepository _repository;
        private readonly IMapper _mapper;
        private readonly DataAccess.ShareItDbContext _context;

        public CompensationDisputeService(ICompensationDisputeRepository repository, IMapper mapper, DataAccess.ShareItDbContext context)
        {
            _repository = repository;
            _mapper = mapper;
            _context = context;
        }

        public async Task<List<DisputeCaseListDto>> GetPendingDisputesAsync()
        {
            return await _repository.GetPendingDisputeCasesAsync();
        }

        public async Task<DisputeCaseDetailDto?> GetDisputeDetailAsync(Guid violationId)
        {
            return await _repository.GetDisputeCaseDetailAsync(violationId);
        }

        public async Task<IssueResolutionResponseDto> CreateAdminResolutionAsync(CreateIssueResolutionDto dto, Guid adminId)
        {
            // Kiểm tra violation có tồn tại không
            if (!await _repository.ViolationExistsAsync(dto.ViolationId))
            {
                throw new ArgumentException("Violation not found");
            }

            // Lấy thông tin violation để xử lý
            var violation = await _repository.GetViolationAsync(dto.ViolationId);
            if (violation == null)
            {
                throw new ArgumentException("Violation not found");
            }

            // Xử lý theo resolution type
            decimal customerFineAmount = dto.CustomerFineAmount;
            decimal providerCompensationAmount = dto.ProviderCompensationAmount;

            if (dto.ResolutionType == ResolutionType.REJECT_CLAIM)
            {
                // Reject Claim: Không có phạt, customer được hoàn lại toàn bộ deposit
                customerFineAmount = 0;
                providerCompensationAmount = 0;
                
                // Cập nhật penalty amount của violation về 0
                violation.PenaltyAmount = 0;
            }
            else if (dto.ResolutionType == ResolutionType.UPHOLD_CLAIM)
            {
                // Uphold Claim: Giữ nguyên penalty amount của provider
                customerFineAmount = violation.PenaltyAmount;
                providerCompensationAmount = violation.PenaltyAmount;
            }
            else if (dto.ResolutionType == ResolutionType.COMPROMISE)
            {
                // Compromise: Dùng số tiền admin nhập vào
                customerFineAmount = dto.CustomerFineAmount;
                providerCompensationAmount = dto.ProviderCompensationAmount;
                
                // Cập nhật penalty amount của violation
                violation.PenaltyAmount = customerFineAmount;
            }

            // Tạo resolution
            var resolution = new IssueResolution
            {
                Id = Guid.NewGuid(),
                ViolationId = dto.ViolationId,
                ResolutionType = dto.ResolutionType,
                CustomerFineAmount = customerFineAmount,
                ProviderCompensationAmount = providerCompensationAmount,
                Reason = dto.Reason,
                ResolutionStatus = ResolutionStatus.COMPLETED,
                ProcessedAt = DateTime.UtcNow,
                ProcessedByAdminId = adminId
            };

            var createdResolution = await _repository.CreateResolutionAsync(resolution);

            // Cập nhật trạng thái violation
            await _repository.UpdateViolationStatusAsync(dto.ViolationId, ViolationStatus.RESOLVED_BY_ADMIN);

            // Cập nhật violation penalty amount
            await _repository.UpdateViolationAsync(violation);

            // Tạo hoặc cập nhật DepositRefund record
            var orderId = violation.OrderItem.OrderId;
            var existingRefund = await _context.DepositRefunds
                .FirstOrDefaultAsync(dr => dr.OrderId == orderId);

            if (existingRefund != null)
            {
                // Cập nhật refund hiện có
                existingRefund.TotalPenaltyAmount = customerFineAmount;
                existingRefund.RefundAmount = existingRefund.OriginalDepositAmount - customerFineAmount;
                existingRefund.Notes = $"Updated after admin resolution: {dto.ResolutionType}. Reason: {dto.Reason}";
                _context.DepositRefunds.Update(existingRefund);
            }
            else
            {
                // Tạo refund mới
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order != null)
                {
                    var totalDeposit = order.Items.Sum(i => i.DepositPerUnit * i.Quantity);
                    
                    var depositRefund = new DepositRefund
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        CustomerId = order.CustomerId,
                        OriginalDepositAmount = totalDeposit,
                        TotalPenaltyAmount = customerFineAmount,
                        RefundAmount = totalDeposit - customerFineAmount,
                        Status = TransactionStatus.initiated,
                        Notes = $"Refund after admin resolution: {dto.ResolutionType}. Reason: {dto.Reason}",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.DepositRefunds.AddAsync(depositRefund);
                }
            }

            await _context.SaveChangesAsync();

            return _mapper.Map<IssueResolutionResponseDto>(createdResolution);
        }
    }
}

