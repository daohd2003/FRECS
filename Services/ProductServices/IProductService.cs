using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProductServices
{
    public interface IProductService
    {
        IQueryable<ProductDTO> GetAll();
        Task<ProductDTO?> GetByIdAsync(Guid id);
        Task<ProductDTO> AddAsync(ProductDTO productDto);
        Task<bool> UpdateAsync(ProductDTO productDto);
        Task<bool> DeleteAsync(Guid id);
    }
}
