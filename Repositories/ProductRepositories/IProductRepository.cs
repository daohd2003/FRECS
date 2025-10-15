using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ProductRepositories
{
    public interface IProductRepository : IRepository<Product>
    {
        IQueryable<Product> GetAllWithIncludes();
        Task<IEnumerable<Product>> GetProductsWithImagesAsync();
        Task<Product?> GetProductWithImagesByIdAsync(Guid id);
        Task<bool> IsProductAvailable(Guid productId, DateTime startDate, DateTime endDate);
        Task<Product> AddProductWithImagesAsync(ProductRequestDTO dto);
        Task<bool> UpdateProductWithImagesAsync(ProductDTO productDto);
        Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request);
        Task<bool> HasOrderItemsAsync(Guid productId);
    }
}
