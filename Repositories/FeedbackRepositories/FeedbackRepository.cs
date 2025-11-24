using AutoMapper;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.FeedbackRepositories
{
    public class FeedbackRepository : Repository<Feedback>, IFeedbackRepository
    {
        public FeedbackRepository(ShareItDbContext context, IMapper mapper) : base(context) { }

        /// <summary>
        /// Lấy feedback theo ID với đầy đủ thông tin liên quan
        /// Include: Customer, Product, Order, OrderItem, ProviderResponder
        /// </summary>
        /// <param name="id">Feedback ID</param>
        /// <returns>Feedback entity với đầy đủ thông tin, null nếu không tồn tại</returns>
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

        /// <summary>
        /// Lấy tất cả feedback cho một target cụ thể (Product hoặc Order)
        /// Sắp xếp theo thời gian tạo mới nhất
        /// </summary>
        /// <param name="targetType">Loại target (Product hoặc Order)</param>
        /// <param name="targetId">ID của target</param>
        /// <returns>Danh sách Feedback entities</returns>
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

        /// <summary>
        /// Kiểm tra customer đã feedback cho OrderItem này chưa
        /// Dùng để validate không cho feedback trùng
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <param name="orderItemId">ID order item</param>
        /// <returns>true nếu đã feedback, false nếu chưa</returns>
        public async Task<bool> HasUserFeedbackedOrderItemAsync(Guid customerId, Guid orderItemId)
        {
            return await _context.Feedbacks
                .AnyAsync(f => f.TargetType == FeedbackTargetType.Product && f.CustomerId == customerId && f.OrderItemId == orderItemId);
        }

        /// <summary>
        /// Kiểm tra customer đã feedback cho Order này chưa
        /// Dùng để validate không cho feedback trùng
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <param name="orderId">ID đơn hàng</param>
        /// <returns>true nếu đã feedback, false nếu chưa</returns>
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
        public async Task<PaginatedResponse<Feedback>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize)
        {
            var query = _context.Feedbacks
                .Where(f => f.ProductId == productId)
                .Include(f => f.Customer).ThenInclude(c => c.Profile) // Include để mapping CustomerName
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile) // Include để mapping ProviderResponderName
                .OrderByDescending(f => f.CreatedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<Feedback>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        /// <summary>
        /// Lấy tất cả feedback của một customer cho một sản phẩm cụ thể
        /// </summary>
        public async Task<IEnumerable<Feedback>> GetFeedbacksByProductAndCustomerAsync(Guid productId, Guid customerId)
        {
            return await _context.Feedbacks
                .Where(f => f.ProductId == productId && f.CustomerId == customerId)
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product)
                .Include(f => f.Order)
                .Include(f => f.OrderItem)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        // Feedback Management Methods
        public async Task<PaginatedResponse<Feedback>> GetAllFeedbacksWithFilterAsync(FeedbackFilterDto filter)
        {
            var query = _context.Feedbacks
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product).ThenInclude(p => p.Images)
                .Include(f => f.Product).ThenInclude(p => p.Provider).ThenInclude(pr => pr.Profile)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .Include(f => f.BlockedBy).ThenInclude(b => b.Profile)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(f => 
                    f.Comment.Contains(filter.SearchTerm) ||
                    f.Customer.Profile.FullName.Contains(filter.SearchTerm) ||
                    (f.Product != null && f.Product.Name.Contains(filter.SearchTerm))
                );
            }

            if (filter.Rating.HasValue && filter.Rating.Value > 0)
            {
                query = query.Where(f => f.Rating == filter.Rating.Value);
            }

            if (!string.IsNullOrEmpty(filter.ResponseStatus))
            {
                if (filter.ResponseStatus == "Responded")
                    query = query.Where(f => f.ProviderResponse != null);
                else if (filter.ResponseStatus == "NoResponse")
                    query = query.Where(f => f.ProviderResponse == null);
            }

            if (!string.IsNullOrEmpty(filter.TimeFilter))
            {
                var now = DateTime.UtcNow;
                switch (filter.TimeFilter)
                {
                    case "Today":
                        query = query.Where(f => f.CreatedAt.Date == now.Date);
                        break;
                    case "ThisWeek":
                        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
                        query = query.Where(f => f.CreatedAt >= startOfWeek);
                        break;
                    case "ThisMonth":
                        var startOfMonth = new DateTime(now.Year, now.Month, 1);
                        query = query.Where(f => f.CreatedAt >= startOfMonth);
                        break;
                }
            }

            if (filter.IsBlocked.HasValue)
            {
                query = query.Where(f => f.IsBlocked == filter.IsBlocked.Value);
            }

            // Sorting
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = filter.SortBy switch
                {
                    "Rating" => filter.SortOrder == "asc" 
                        ? query.OrderBy(f => f.Rating) 
                        : query.OrderByDescending(f => f.Rating),
                    _ => filter.SortOrder == "asc" 
                        ? query.OrderBy(f => f.CreatedAt) 
                        : query.OrderByDescending(f => f.CreatedAt)
                };
            }
            else
            {
                query = query.OrderByDescending(f => f.CreatedAt);
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResponse<Feedback>
            {
                Items = items,
                Page = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalItems = totalItems
            };
        }

        public async Task<Feedback?> GetFeedbackDetailAsync(Guid feedbackId)
        {
            return await _context.Feedbacks
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product).ThenInclude(p => p.Provider).ThenInclude(pr => pr.Profile)
                .Include(f => f.Product).ThenInclude(p => p.Images)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .Include(f => f.BlockedBy).ThenInclude(b => b.Profile)
                .FirstOrDefaultAsync(f => f.Id == feedbackId);
        }

        public async Task<bool> BlockFeedbackAsync(Guid feedbackId, Guid blockedById)
        {
            var feedback = await _context.Feedbacks.FindAsync(feedbackId);
            if (feedback == null) return false;

            feedback.IsBlocked = true;
            feedback.IsVisible = false;
            feedback.BlockedAt = DateTime.UtcNow;
            feedback.BlockedById = blockedById;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnblockFeedbackAsync(Guid feedbackId)
        {
            var feedback = await _context.Feedbacks.FindAsync(feedbackId);
            if (feedback == null) return false;

            feedback.IsBlocked = false;
            feedback.IsVisible = true;
            feedback.BlockedAt = null;
            feedback.BlockedById = null;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FeedbackStatisticsDto> GetFeedbackStatisticsAsync()
        {
            var totalFeedbacks = await _context.Feedbacks.CountAsync();
            var averageRating = totalFeedbacks > 0 
                ? await _context.Feedbacks.AverageAsync(f => (double)f.Rating) 
                : 0;
            var flaggedContent = await _context.Feedbacks.CountAsync(f => f.IsBlocked);
            var respondedCount = await _context.Feedbacks.CountAsync(f => f.ProviderResponse != null);
            var blockedCount = await _context.Feedbacks.CountAsync(f => f.IsBlocked);

            return new FeedbackStatisticsDto
            {
                TotalFeedbacks = totalFeedbacks,
                AverageRating = Math.Round(averageRating, 1),
                FlaggedContent = flaggedContent,
                RespondedCount = respondedCount,
                BlockedCount = blockedCount
            };
        }

        public async Task<IEnumerable<Feedback>> GetFlaggedFeedbacksAsync()
        {
            return await _context.Feedbacks
                .Where(f => f.IsBlocked)
                .Include(f => f.Customer).ThenInclude(c => c.Profile)
                .Include(f => f.Product).ThenInclude(p => p.Provider).ThenInclude(pr => pr.Profile)
                .Include(f => f.ProviderResponder).ThenInclude(pr => pr.Profile)
                .Include(f => f.BlockedBy).ThenInclude(b => b.Profile)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }
    }
}
