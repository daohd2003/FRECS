using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.ProductRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProductServices
{
    public class ProductService : IProductService
    {
        private readonly ShareItDbContext _context;
        private readonly IMapper _mapper;

        public ProductService(ShareItDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public IQueryable<ProductDTO> GetAll()
        {
            return _context.Products
                .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider);
        }

        public async Task<ProductDTO?> GetByIdAsync(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Provider)
                .ThenInclude(prov => prov.Profile)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            return product == null ? null : _mapper.Map<ProductDTO>(product);
        }

        public async Task<ProductDTO> AddAsync(ProductDTO productDto)
        {
            var product = _mapper.Map<Product>(productDto);
            product.Id = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            return _mapper.Map<ProductDTO>(product);
        }

        public async Task<bool> UpdateAsync(ProductDTO productDto)
        {
            var existingProduct = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productDto.Id);

            if (existingProduct == null) return false;

            // Map các trường từ DTO sang entity, giữ lại các trường không map nếu cần
            _mapper.Map(productDto, existingProduct);

            existingProduct.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(existingProduct);
            var updated = await _context.SaveChangesAsync();

            return updated > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            _context.Products.Remove(product);
            var deleted = await _context.SaveChangesAsync();

            return deleted > 0;
        }
    }
}
