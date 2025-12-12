using AutoMapper;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using BusinessObject.Utilities;
using System.Text.Json;

namespace Repositories.ReportRepositories
{
    public class ReportRepository : Repository<Report>, IReportRepository
    {
        public ReportRepository(ShareItDbContext context) : base(context) { }

        public async Task<IEnumerable<Report>> GetReportsByReporterIdAsync(Guid reporterId)
        {
            return await _context.Reports
                .Where(r => r.ReporterId == reporterId)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByReporteeIdAsync(Guid reporteeId)
        {
            return await _context.Reports
                .Where(r => r.ReporteeId == reporteeId)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        public async Task<IEnumerable<Report>> GetReportsByStatusAsync(ReportStatus status)
        {
            return await _context.Reports
                .Where(r => r.Status == status)
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .ToListAsync();
        }

        // THAY ĐỔI: Sửa lại phương thức này để trả về đối tượng Report đầy đủ thông tin
        // Giúp cho AutoMapper ở tầng Service hoạt động
        public async Task<Report?> GetReportWithDetailsAsync(Guid reportId)
        {
            return await _context.Reports
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .Include(r => r.AssignedAdmin).ThenInclude(u => u.Profile) // Lấy cả thông tin admin được gán
                .Include(r => r.Order).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images) // Lấy thông tin order, products và images
                .Include(r => r.OrderItem).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images) // Lấy thông tin OrderItem cụ thể
                .FirstOrDefaultAsync(r => r.Id == reportId);
        }

        public async Task CreateReportAsync(ReportDTO dto)
        {
            // Kiểm tra nếu report về OrderItem, cần validate trạng thái đơn hàng
            if (dto.ReportType == ReportType.OrderItem)
            {
                if (!dto.OrderItemId.HasValue)
                    throw new ArgumentException("OrderItemId is required for OrderItem reports");

                var orderItem = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .FirstOrDefaultAsync(oi => oi.Id == dto.OrderItemId.Value);
                
                if (orderItem == null)
                    throw new ArgumentException("OrderItem not found");
                
                if (orderItem.Order.Status != OrderStatus.in_use)
                    throw new InvalidOperationException("Can only report products when order is in use");
                
                // Tự động set OrderId từ OrderItem
                dto.OrderId = orderItem.OrderId;
            }
            // Kiểm tra nếu report về toàn bộ Order
            else if (dto.ReportType == ReportType.Order)
            {
                if (!dto.OrderId.HasValue)
                    throw new ArgumentException("OrderId is required for Order reports");

                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == dto.OrderId.Value);
                
                if (order == null)
                    throw new ArgumentException("Order not found");
                
                if (order.Status != OrderStatus.in_use)
                    throw new InvalidOperationException("Can only report orders when order is in use");
                
                // Đảm bảo OrderItemId là null cho Order report
                dto.OrderItemId = null;
            }

            // Logic tạo Report với hỗ trợ OrderId, OrderItemId và EvidenceImages
            var report = new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = dto.ReporterId,
                ReporteeId = dto.ReporteeId,
                OrderId = dto.OrderId,
                OrderItemId = dto.OrderItemId,
                ReportType = dto.ReportType,
                Subject = dto.Subject,
                Description = dto.Description,
                Status = ReportStatus.open,
                CreatedAt = DateTimeHelper.GetVietnamTime(),
                Priority = dto.Priority,
                EvidenceImages = dto.EvidenceImages != null && dto.EvidenceImages.Any() 
                    ? JsonSerializer.Serialize(dto.EvidenceImages) 
                    : null
            };
            await AddAsync(report);
        }

        public async Task UpdateReportStatusAsync(Guid reportId, ReportStatus newStatus)
        {
            var report = await GetByIdAsync(reportId);
            if (report == null) return;

            report.Status = newStatus;
            await UpdateAsync(report);
        }
        public IQueryable<Report> GetReportsAsQueryable()
        {
            return _context.Reports
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .Include(r => r.AssignedAdmin).ThenInclude(u => u.Profile)
                .Include(r => r.Order).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images)
                .Include(r => r.OrderItem).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images)
                .AsSplitQuery()
                .AsQueryable();
        }
    }
}
