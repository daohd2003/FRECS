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
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
            .ToListAsync();
        }

        public async Task<Product?> GetProductWithImagesByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}
