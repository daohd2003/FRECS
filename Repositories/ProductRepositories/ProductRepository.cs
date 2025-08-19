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

namespace Repositories.ProductRepositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Product>> GetProductsWithImagesAsync()
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
            .ToListAsync();
        }

        public async Task<Product?> GetProductWithImagesByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> IsProductAvailable(Guid productId, DateTime startDate, DateTime endDate)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.AvailabilityStatus != AvailabilityStatus.available)
            {
                return false;
            }

            var conflictingOrders = await _context.Orders
                .Where(o =>
                    o.Items.Any(oi => oi.ProductId == productId) &&
                    (
                        // Check for overlapping date ranges
                        (startDate < o.RentalEnd && endDate > o.RentalStart)
                    ) &&
                    (
                        o.Status != OrderStatus.cancelled &&
                        o.Status != OrderStatus.returned
                    )
                )
                .AnyAsync();

            return !conflictingOrders;
        }
    }
}
