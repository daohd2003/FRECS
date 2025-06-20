using AutoMapper;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.FeedbackRepositories
{
    public class FeedbackRepository : Repository<Feedback>, IFeedbackRepository
    {
        public FeedbackRepository(ShareItDbContext context, IMapper mapper) : base(context) { }

        public override async Task<Feedback?> GetByIdAsync(Guid id)
        {
            return await _context.Feedbacks
                .Include(f => f.Customer).ThenInclude(c => c.Profile) 
                .Include(f => f.Product)
                .Include(f => f.Order)
                .Include(f => f.OrderItem)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile) 
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<IEnumerable<Feedback>> GetFeedbacksByTargetAsync(FeedbackTargetType targetType, Guid targetId)
        {
            var query = _context.Feedbacks.AsQueryable();

            if (targetType == FeedbackTargetType.Product)
            {
                query = query.Where(f => f.TargetType == FeedbackTargetType.Product && f.ProductId == targetId);
            }
            else if (targetType == FeedbackTargetType.Order)
            {
                query = query.Where(f => f.TargetType == FeedbackTargetType.Order && f.OrderId == targetId);
            }

            return await query
                .Include(f => f.Customer).ThenInclude(c => c.Profile) 
                .Include(f => f.Product)
                .Include(f => f.Order)
                .Include(f => f.OrderItem)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Feedback>> GetFeedbacksByCustomerIdAsync(Guid customerId)
        {
            return await _context.Feedbacks
                .Where(f => f.CustomerId == customerId)
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product)
                .Include(f => f.Order)
                .Include(f => f.OrderItem)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile) 
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasUserFeedbackedOrderItemAsync(Guid customerId, Guid orderItemId)
        {
            return await _context.Feedbacks
                .AnyAsync(f => f.TargetType == FeedbackTargetType.Product && f.CustomerId == customerId && f.OrderItemId == orderItemId);
        }

        public async Task<bool> HasUserFeedbackedOrderAsync(Guid customerId, Guid orderId)
        {
            return await _context.Feedbacks
                .AnyAsync(f => f.TargetType == FeedbackTargetType.Order && f.CustomerId == customerId && f.OrderId == orderId);
        }
        public async Task<IEnumerable<Feedback>> GetFeedbacksByProviderIdAsync(Guid providerId)
        {
            return await _context.Feedbacks
                .Where(f => (f.TargetType == FeedbackTargetType.Product && f.Product != null && f.Product.ProviderId == providerId) ||
                            (f.TargetType == FeedbackTargetType.Order && f.Order != null && f.Order.ProviderId == providerId))
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product)
                .Include(f => f.Order)
                .Include(f => f.OrderItem)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }
    }
}
