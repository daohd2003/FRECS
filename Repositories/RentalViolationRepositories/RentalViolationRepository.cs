using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.RentalViolationRepositories
{
    public class RentalViolationRepository : Repository<RentalViolation>, IRentalViolationRepository
    {
        public RentalViolationRepository(ShareItDbContext context) : base(context)
        {
        }

        public override async Task<RentalViolation?> GetByIdAsync(Guid id)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(rv => rv.Images)
                .FirstOrDefaultAsync(rv => rv.ViolationId == id);
        }

        public async Task<RentalViolation?> GetViolationWithDetailsAsync(Guid violationId)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                        .ThenInclude(o => o.Provider)
                .Include(rv => rv.Images)
                .FirstOrDefaultAsync(rv => rv.ViolationId == violationId);
        }

        public async Task<IEnumerable<RentalViolation>> GetViolationsByOrderIdAsync(Guid orderId)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(rv => rv.Images)
                .Where(rv => rv.OrderItem.OrderId == orderId)
                .OrderByDescending(rv => rv.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<RentalViolation>> GetViolationsByOrderItemIdAsync(Guid orderItemId)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(rv => rv.Images)
                .Where(rv => rv.OrderItemId == orderItemId)
                .OrderByDescending(rv => rv.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<RentalViolation>> GetViolationsByProviderIdAsync(Guid providerId)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Include(rv => rv.Images)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId)
                .OrderByDescending(rv => rv.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<RentalViolation>> GetViolationsByCustomerIdAsync(Guid customerId)
        {
            return await _context.RentalViolations
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Include(rv => rv.Images)
                .Where(rv => rv.OrderItem.Order.CustomerId == customerId)
                .OrderByDescending(rv => rv.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateViolationAsync(RentalViolation violation)
        {
            try
            {
                violation.UpdatedAt = DateTime.UtcNow;
                _context.RentalViolations.Update(violation);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task AddEvidenceImageAsync(RentalViolationImage image)
        {
            await _context.RentalViolationImages.AddAsync(image);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<RentalViolationImage>> GetEvidenceImagesAsync(Guid violationId)
        {
            return await _context.RentalViolationImages
                .Where(i => i.ViolationId == violationId)
                .OrderBy(i => i.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetAdminUsersAsync()
        {
            return await _context.Users
                .Where(u => u.Role == BusinessObject.Enums.UserRole.admin)
                .ToListAsync();
        }
    }
}