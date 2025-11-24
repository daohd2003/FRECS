using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.OrderRepositories
{
    public class OrderRepository : Repository<Order>, IOrderRepository
    {
        private readonly IMapper _mapper;

        public OrderRepository(ShareItDbContext context, IMapper mapper) : base(context)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersDetailAsync()
        {
            return await _context.Orders
                .ProjectTo<OrderDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy order theo ID với OrderItems
        /// Dùng cho xem chi tiết đơn hàng, cập nhật trạng thái
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Order entity với Items, null nếu không tồn tại</returns>
        public override async Task<Order?> GetByIdAsync(Guid id)
        {
            return await _context.Orders
              .Include(o => o.Items) // Eager loading OrderItems
                  .FirstOrDefaultAsync(o => o.Id == id);
        }

        /// <summary>
        /// Lấy tất cả orders theo trạng thái với đầy đủ thông tin
        /// Dùng cho admin/staff quản lý đơn hàng theo trạng thái
        /// </summary>
        /// <param name="status">Trạng thái đơn hàng (pending, approved, shipping, etc.)</param>
        /// <returns>Danh sách OrderWithDetailsDto</returns>
        public async Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status)
        {
            return await _context.Orders
                .Where(o => o.Status == status) // Filter theo trạng thái
                .Include(o => o.Items) // Include OrderItems
                .Include(o => o.Customer) // Include Customer info
                .Include(o => o.Provider) // Include Provider info
                .ProjectTo<OrderWithDetailsDto>(_mapper.ConfigurationProvider) // Map sang DTO
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tất cả orders của một provider
        /// Dùng cho provider xem danh sách đơn hàng của mình
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <returns>Danh sách Order entities, sắp xếp theo thời gian tạo mới nhất</returns>
        public async Task<IEnumerable<Order>> GetByProviderIdAsync(Guid providerId)
        {
            return await _context.Orders
                .Where(o => o.ProviderId == providerId) // Filter theo provider
                .Include(o => o.Items) // Include OrderItems
                .Include(o => o.Customer) // Include Customer info
                .OrderByDescending(o => o.CreatedAt) // Sắp xếp mới nhất trước
                .ToListAsync();
        }

        /// <summary>
        /// Cập nhật order và xử lý OrderItems (thêm/sửa/xóa)
        /// Logic phức tạp: So sánh items hiện tại với items mới để xác định thao tác
        /// </summary>
        /// <param name="order">Order entity cần cập nhật (bao gồm Items)</param>
        /// <returns>true nếu cập nhật thành công</returns>
        public override async Task<bool> UpdateAsync(Order order)
        {
            // Bước 1: Lấy danh sách Id của các item hiện có trong DB
            var existingItemIds = await _context.OrderItems
                .Where(i => i.OrderId == order.Id)
                .Select(i => i.Id)
                .ToListAsync();

            // Bước 2: Lấy danh sách Id của các item hiện tại (từ client gửi lên)
            var currentItemIds = order.Items.Select(i => i.Id).ToList();

            // Bước 3: Xóa những item có trong DB nhưng không có trong client
            // (User đã remove item khỏi order)
            var itemsToRemove = existingItemIds.Except(currentItemIds).ToList();
            foreach (var idToRemove in itemsToRemove)
            {
                var itemToRemove = new OrderItem { Id = idToRemove };
                _context.OrderItems.Attach(itemToRemove);
                _context.OrderItems.Remove(itemToRemove);
            }

            // Bước 4: Cập nhật hoặc thêm mới các item còn lại
            foreach (var item in order.Items)
            {
                // Attach item nếu chưa được track bởi context
                if (_context.Entry(item).State == EntityState.Detached)
                {
                    _context.Attach(item);
                }

                if (item.Id == Guid.Empty)
                {
                    // Item mới (chưa có Id) → Thêm mới
                    item.Id = Guid.NewGuid();
                    _context.Entry(item).State = EntityState.Added;
                }
                else
                {
                    // Item đã tồn tại → Cập nhật
                    _context.Entry(item).State = EntityState.Modified;
                }
            }

            // Bước 5: Cập nhật order
            _context.Orders.Update(order);

            // Bước 6: Lưu tất cả thay đổi vào database
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task UpdateOnlyStatusAndTimeAsync(Order order)
        {
            _context.Orders.Attach(order);
            _context.Entry(order).Property(o => o.Status).IsModified = true;
            _context.Entry(order).Property(o => o.UpdatedAt).IsModified = true;

            await _context.SaveChangesAsync();
        }

        public async Task<Order> GetOrderWithItemsAsync(Guid orderId)
        {
            return await _context.Orders
                                 .Include(o => o.Items)
                                 .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<bool> UpdateOrderContactInfoAsync(Order orderToUpdate)
        {
            if (orderToUpdate == null) return false;

            _context.Orders.Update(orderToUpdate);

            await _context.SaveChangesAsync();
            return true;
        }

        public Task<string?> GetOrderItemId(Guid customerId, Guid productId)
        {
            // Get OrderItem that:
            // 1. Belongs to a completed order (returned for rental, in_use for purchase)
            // 2. Matches the productId
            // 3. Does NOT have feedback yet (not in Feedbacks table)
            
            // Valid statuses for feedback:
            // - returned: Rental orders that have been returned
            // - in_use: Orders that customer has confirmed received (both rental & purchase)
            var validStatuses = new[] { 
                OrderStatus.returned,      // Rental completed
                OrderStatus.in_use         // Customer confirmed received
            };
            
            var orderItemId = _context.Orders
                .Include(o => o.Items)
                .Where(o => o.CustomerId == customerId && validStatuses.Contains(o.Status))
                .OrderByDescending(o => o.CreatedAt)
                .SelectMany(o => o.Items)
                .Where(i => i.ProductId == productId && 
                            !_context.Feedbacks.Any(f => f.OrderItemId == i.Id)) // Filter out items that already have feedback
                .Select(i => i.Id.ToString())
                .FirstOrDefault();

            return Task.FromResult(orderItemId);
        }

        public async Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync()
        {
            return await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.Profile)
                .Include(o => o.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order> GetOrderWithFullDetailsAsync(Guid orderId)
        {
            return await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.Profile)
                .Include(o => o.Provider)
                    .ThenInclude(p => p.Profile)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }
    }
}
