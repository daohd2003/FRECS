using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
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

        /// <summary>
        /// Lấy tất cả sản phẩm AVAILABLE với đầy đủ thông tin (Images, Category, Provider)
        /// Dùng cho customer xem danh sách sản phẩm
        /// AsNoTracking: Tối ưu performance cho read-only queries
        /// </summary>
        /// <returns>IQueryable Product để có thể filter/sort/paginate thêm</returns>
        public IQueryable<Product> GetAllWithIncludes()
        {
            return _context.Products
                .AsNoTracking() // Read-only, không track changes
                .Include(p => p.Images.Where(img => img.IsPrimary || img.ImageUrl != null)) // Chỉ lấy ảnh primary hoặc có URL
                .Include(p => p.Category) // Eager loading Category
                .Include(p => p.Provider) // Eager loading Provider
                    .ThenInclude(u => u.Profile) // Eager loading Provider Profile
                .Where(p => p.AvailabilityStatus == AvailabilityStatus.available); // Chỉ lấy sản phẩm available
        }

        /// <summary>
        /// Lấy TẤT CẢ sản phẩm (không filter status) với đầy đủ thông tin
        /// Dùng cho admin/staff quản lý tất cả sản phẩm (kể cả pending, rejected, archived)
        /// </summary>
        /// <returns>IQueryable Product để có thể filter/sort/paginate thêm</returns>
        public IQueryable<Product> GetAllWithIncludesNoFilter()
        {
            return _context.Products
                .AsNoTracking()
                .Include(p => p.Images.Where(img => img.IsPrimary || img.ImageUrl != null))
                .Include(p => p.Category)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile);
            // Không filter status - trả về TẤT CẢ sản phẩm
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

        /// <summary>
        /// Thêm sản phẩm mới với hình ảnh trong một transaction
        /// Đảm bảo toàn vẹn dữ liệu: Nếu lỗi thì rollback cả product và images
        /// </summary>
        /// <param name="dto">Thông tin sản phẩm và danh sách hình ảnh</param>
        /// <returns>Product entity vừa tạo (đã có Id)</returns>
        /// <exception cref="Exception">Lỗi khi lưu database</exception>
        public async Task<Product> AddProductWithImagesAsync(ProductRequestDTO dto)
        {
            // Bắt đầu transaction để đảm bảo toàn vẹn dữ liệu
            // Nếu có lỗi ở bất kỳ bước nào → Rollback tất cả
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bước 1: Tạo Product Entity từ DTO
                var newProduct = new Product
                {
                    ProviderId = dto.ProviderId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId ?? Guid.Empty,
                    Size = dto.Size,
                    Color = dto.Color,
                    PricePerDay = dto.PricePerDay,
                    PurchasePrice = dto.PurchasePrice ?? 0,
                    PurchaseQuantity = dto.PurchaseQuantity ?? 0,
                    RentalQuantity = dto.RentalQuantity ?? 0,
                    SecurityDeposit = dto.SecurityDeposit,
                    // Parse string sang enum
                    Gender = Enum.Parse<Gender>(dto.Gender ?? "Unisex"),
                    RentalStatus = Enum.Parse<RentalStatus>(dto.RentalStatus ?? "Available"),
                    PurchaseStatus = Enum.Parse<PurchaseStatus>(dto.PurchaseStatus ?? "NotForSale"),
                    CreatedAt = DateTimeHelper.GetVietnamTime(),
                    AvailabilityStatus = AvailabilityStatus.available, // Mặc định available (sẽ được AI check sau)
                    UpdatedAt = DateTimeHelper.GetVietnamTime(),
                    AverageRating = 0, // Chưa có đánh giá
                    RatingCount = 0,
                    IsPromoted = false
                };

                // Bước 2: Lưu Product để database sinh Id
                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync(); // Database sinh Id cho newProduct

                // Bước 3: Tạo ProductImages với ProductId vừa được sinh
                if (dto.Images != null && dto.Images.Any())
                {
                    foreach (var imageDto in dto.Images)
                    {
                        var productImage = new ProductImage
                        {
                            ProductId = newProduct.Id, // Dùng Id vừa được tạo
                            ImageUrl = imageDto.ImageUrl,
                            IsPrimary = imageDto.IsPrimary
                        };
                        _context.ProductImages.Add(productImage);
                    }

                    // Lưu tất cả images
                    await _context.SaveChangesAsync();
                }

                // Bước 4: Commit transaction nếu mọi thứ thành công
                await transaction.CommitAsync();

                return newProduct;
            }
            catch (Exception)
            {
                // Rollback nếu có lỗi (xóa cả product và images đã thêm)
                await transaction.RollbackAsync();
                throw; // Ném lại lỗi để Service/Controller xử lý
            }
        }

        public async Task<bool> UpdateProductWithImagesAsync(ProductDTO productDto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingProduct = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productDto.Id);

                if (existingProduct == null) return false;

                // Cập nhật các trường từ DTO
                existingProduct.Name = productDto.Name;
                existingProduct.Description = productDto.Description;
                existingProduct.CategoryId = productDto.CategoryId ?? Guid.Empty;
                existingProduct.Size = productDto.Size;
                existingProduct.Color = productDto.Color;
                existingProduct.PricePerDay = productDto.PricePerDay;
                existingProduct.PurchasePrice = productDto.PurchasePrice;
                existingProduct.PurchaseQuantity = productDto.PurchaseQuantity;
                existingProduct.RentalQuantity = productDto.RentalQuantity;
                existingProduct.SecurityDeposit = productDto.SecurityDeposit;

                // Parse string sang enum
                existingProduct.Gender = Enum.Parse<Gender>(productDto.Gender ?? "Unisex");
                existingProduct.RentalStatus = Enum.Parse<RentalStatus>(productDto.RentalStatus ?? "Available");
                existingProduct.PurchaseStatus = Enum.Parse<PurchaseStatus>(productDto.PurchaseStatus ?? "NotForSale");
                existingProduct.AvailabilityStatus = Enum.Parse<AvailabilityStatus>(productDto.AvailabilityStatus ?? "available");

                existingProduct.UpdatedAt = DateTimeHelper.GetVietnamTime();

                // Xử lý Images riêng biệt
                if (productDto.Images != null)
                {
                    // Xóa tất cả images cũ
                    _context.ProductImages.RemoveRange(existingProduct.Images);

                    // Thêm images mới
                    foreach (var imageDto in productDto.Images)
                    {
                        var productImage = new ProductImage
                        {
                            ProductId = existingProduct.Id,
                            ImageUrl = imageDto.ImageUrl,
                            IsPrimary = imageDto.IsPrimary
                        };
                        _context.ProductImages.Add(productImage);
                    }
                }

                _context.Products.Update(existingProduct);
                var updated = await _context.SaveChangesAsync();

                if (updated > 0)
                {
                    await transaction.CommitAsync();
                    return true;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request)
        {
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null) return false;

            product.UpdatedAt = DateTimeHelper.GetVietnamTime();
            if (request.NewAvailabilityStatus.Equals("Approved"))
            {
                product.AvailabilityStatus = AvailabilityStatus.available;
            }
            else
            {
                product.AvailabilityStatus = AvailabilityStatus.rejected;
            }

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> HasOrderItemsAsync(Guid productId)
        {
            return await _context.OrderItems.AnyAsync(oi => oi.ProductId == productId);
        }

        public async Task<Product?> GetProductWithProviderByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Provider)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> UpdateProductAvailabilityStatusAsync(Guid productId, AvailabilityStatus status)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return false;

            product.AvailabilityStatus = status;
            product.UpdatedAt = DateTimeHelper.GetVietnamTime();

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Product?> GetProductWithImagesAndProviderAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Provider)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}
