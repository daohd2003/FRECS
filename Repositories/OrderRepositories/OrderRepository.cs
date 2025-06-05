using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.OrdersDto;
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

        public override async Task<Order?> GetByIdAsync(Guid id)
        {
            return await _context.Orders
              .Include(o => o.Items)
                  .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<IEnumerable<OrderWithDetailsDto>> GetOrdersByStatusAsync(OrderStatus status)
        {
            return await _context.Orders
                .Where(o => o.Status == status)
                .Include(o => o.Items)
                .Include(o => o.Customer)
                .Include(o => o.Provider)
                .ProjectTo<OrderWithDetailsDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetByProviderIdAsync(Guid providerId)
        {
            return await _context.Orders
                .Where(o => o.ProviderId == providerId)
                .Include(o => o.Items)
                .Include(o => o.Customer)
                .ToListAsync();
        }

        public override async Task<bool> UpdateAsync(Order order)
        {
            // Lấy danh sách Id của các item hiện có trong DB
            var existingItemIds = await _context.OrderItems
                .Where(i => i.OrderId == order.Id)
                .Select(i => i.Id)
                .ToListAsync();

            // Các Id item hiện tại trong order.Items (từ client gửi lên)
            var currentItemIds = order.Items.Select(i => i.Id).ToList();

            // Xóa những item có trong DB nhưng không có trong client gửi lên (tức là bị remove)
            var itemsToRemove = existingItemIds.Except(currentItemIds).ToList();
            foreach (var idToRemove in itemsToRemove)
            {
                var itemToRemove = new OrderItem { Id = idToRemove };
                _context.OrderItems.Attach(itemToRemove);
                _context.OrderItems.Remove(itemToRemove);
            }

            // Cập nhật trạng thái các item còn lại (add hoặc update)
            foreach (var item in order.Items)
            {
                if (_context.Entry(item).State == EntityState.Detached)
                {
                    _context.Attach(item);
                }

                if (item.Id == Guid.Empty)
                {
                    // Item mới
                    item.Id = Guid.NewGuid(); // Phát sinh Id mới nếu chưa có
                    _context.Entry(item).State = EntityState.Added;
                }
                else
                {
                    _context.Entry(item).State = EntityState.Modified;
                }
            }

            // Update order
            _context.Orders.Update(order);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
