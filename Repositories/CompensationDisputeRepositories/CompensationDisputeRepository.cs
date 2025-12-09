using BusinessObject.DTOs.IssueResolutionDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.CompensationDisputeRepositories
{
    public class CompensationDisputeRepository : ICompensationDisputeRepository
    {
        private readonly ShareItDbContext _context;

        public CompensationDisputeRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<List<DisputeCaseListDto>> GetPendingDisputeCasesAsync()
        {
            return await _context.RentalViolations
                .Where(v => v.Status == ViolationStatus.PENDING_ADMIN_REVIEW)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Provider)
                            .ThenInclude(p => p.Profile)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                            .ThenInclude(c => c.Profile)
                .OrderBy(v => v.CreatedAt) // Oldest first
                .Select(v => new DisputeCaseListDto
                {
                    ViolationId = v.ViolationId,
                    ProductName = v.OrderItem.Product.Name,
                    ProductImageUrl = v.OrderItem.Product.Images.FirstOrDefault()!.ImageUrl ?? "",
                    ProviderName = v.OrderItem.Order.Provider.Profile != null 
                        ? v.OrderItem.Order.Provider.Profile.FullName 
                        : v.OrderItem.Order.Provider.Email,
                    ProviderId = v.OrderItem.Order.ProviderId,
                    CustomerName = v.OrderItem.Order.Customer.Profile != null 
                        ? v.OrderItem.Order.Customer.Profile.FullName 
                        : v.OrderItem.Order.Customer.Email,
                    CustomerId = v.OrderItem.Order.CustomerId,
                    ComplaintDate = v.CreatedAt,
                    Status = v.Status.ToString(),
                    RequestedCompensation = v.PenaltyAmount
                })
                .ToListAsync();
        }

        public async Task<DisputeCaseDetailDto?> GetDisputeCaseDetailAsync(Guid violationId)
        {
            var violation = await _context.RentalViolations
                .Where(v => v.ViolationId == violationId)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Provider)
                            .ThenInclude(pr => pr.Profile)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Provider)
                            .ThenInclude(p => p.Profile)
                .Include(v => v.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                            .ThenInclude(c => c.Profile)
                .Include(v => v.Images)
                .FirstOrDefaultAsync();

            if (violation == null)
                return null;

            // Get messages related to this violation (if any)
            var messages = await _context.Messages
                .Where(m => (m.SenderId == violation.OrderItem.Order.ProviderId && m.ReceiverId == violation.OrderItem.Order.CustomerId)
                         || (m.SenderId == violation.OrderItem.Order.CustomerId && m.ReceiverId == violation.OrderItem.Order.ProviderId))
                .Include(m => m.Sender)
                    .ThenInclude(s => s.Profile)
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.Profile != null ? m.Sender.Profile.FullName : m.Sender.Email,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    Attachment = string.IsNullOrEmpty(m.AttachmentUrl) ? null : new MessageAttachmentDto
                    {
                        Url = m.AttachmentUrl,
                        Type = m.AttachmentType,
                        MimeType = m.MimeType,
                        FileName = m.FileName
                    }
                })
                .ToListAsync();

            return new DisputeCaseDetailDto
            {
                ViolationId = violation.ViolationId,
                ViolationType = violation.ViolationType,
                DamageDescription = violation.Description,
                DamagePercentage = violation.DamagePercentage,
                RequestedCompensation = violation.PenaltyAmount,
                CurrentPenaltyAmount = violation.PenaltyAmount,
                Status = violation.Status,
                CreatedAt = violation.CreatedAt,
                CustomerNotes = violation.CustomerNotes,
                CustomerResponseAt = violation.CustomerResponseAt,
                ProviderEscalationReason = violation.ProviderEscalationReason,
                CustomerEscalationReason = violation.CustomerEscalationReason,
                Product = new ProductInfoDto
                {
                    Id = violation.OrderItem.Product.Id,
                    Name = violation.OrderItem.Product.Name,
                    ImageUrl = violation.OrderItem.Product.Images.FirstOrDefault()?.ImageUrl ?? "",
                    Value = violation.OrderItem.Product.PurchasePrice > 0 
                        ? violation.OrderItem.Product.PurchasePrice 
                        : violation.OrderItem.Product.SecurityDeposit,
                    ShopName = violation.OrderItem.Product.Provider?.Profile?.FullName 
                        ?? violation.OrderItem.Product.Provider?.Email 
                        ?? "N/A",
                    CompensationPolicy = "Standard compensation policy applies" // Default policy
                },
                Provider = new UserInfoDto
                {
                    Id = violation.OrderItem.Order.ProviderId,
                    Name = violation.OrderItem.Order.Provider.Profile != null 
                        ? violation.OrderItem.Order.Provider.Profile.FullName 
                        : violation.OrderItem.Order.Provider.Email,
                    Email = violation.OrderItem.Order.Provider.Email,
                    ProfilePictureUrl = violation.OrderItem.Order.Provider.Profile?.ProfilePictureUrl
                },
                Customer = new UserInfoDto
                {
                    Id = violation.OrderItem.Order.CustomerId,
                    Name = violation.OrderItem.Order.Customer.Profile != null 
                        ? violation.OrderItem.Order.Customer.Profile.FullName 
                        : violation.OrderItem.Order.Customer.Email,
                    Email = violation.OrderItem.Order.Customer.Email,
                    ProfilePictureUrl = violation.OrderItem.Order.Customer.Profile?.ProfilePictureUrl
                },
                ProviderEvidence = violation.Images
                    .Where(i => i.UploadedBy == EvidenceUploadedBy.PROVIDER)
                    .Select(i => new EvidenceDto
                    {
                        ImageId = i.ImageId,
                        ImageUrl = i.ImageUrl,
                        FileType = i.FileType,
                        UploadedAt = i.UploadedAt,
                        UploadedBy = i.UploadedBy
                    })
                    .ToList(),
                CustomerEvidence = violation.Images
                    .Where(i => i.UploadedBy == EvidenceUploadedBy.CUSTOMER)
                    .Select(i => new EvidenceDto
                    {
                        ImageId = i.ImageId,
                        ImageUrl = i.ImageUrl,
                        FileType = i.FileType,
                        UploadedAt = i.UploadedAt,
                        UploadedBy = i.UploadedBy
                    })
                    .ToList(),
                Messages = messages,
                OrderItem = new OrderItemInfoDto
                {
                    OrderItemId = violation.OrderItem.Id,
                    OrderId = violation.OrderItem.OrderId,
                    Quantity = violation.OrderItem.Quantity,
                    DepositPerUnit = violation.OrderItem.DepositPerUnit,
                    TotalDeposit = violation.OrderItem.DepositPerUnit * violation.OrderItem.Quantity,
                    RentalStartDate = violation.OrderItem.Order.RentalStart ?? DateTime.MinValue,
                    RentalEndDate = violation.OrderItem.Order.RentalEnd ?? DateTime.MinValue
                }
            };
        }

        public async Task<IssueResolution> CreateResolutionAsync(IssueResolution resolution)
        {
            _context.IssueResolutions.Add(resolution);
            await _context.SaveChangesAsync();
            return resolution;
        }

        public async Task<bool> UpdateViolationStatusAsync(Guid violationId, ViolationStatus status)
        {
            var violation = await _context.RentalViolations.FindAsync(violationId);
            if (violation == null)
                return false;

            violation.Status = status;
            violation.UpdatedAt = DateTimeHelper.GetVietnamTime();
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ViolationExistsAsync(Guid violationId)
        {
            return await _context.RentalViolations.AnyAsync(v => v.ViolationId == violationId);
        }

        public async Task<RentalViolation?> GetViolationAsync(Guid violationId)
        {
            return await _context.RentalViolations
                .Include(v => v.OrderItem)
                .FirstOrDefaultAsync(v => v.ViolationId == violationId);
        }

        public async Task<bool> UpdateViolationAsync(RentalViolation violation)
        {
            try
            {
                _context.RentalViolations.Update(violation);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

