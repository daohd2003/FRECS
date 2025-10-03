using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.FavoriteRepositories
{
    public class FavoriteRepository : Repository<Favorite>, IFavoriteRepository
    {
        private readonly IMapper _mapper;
        public FavoriteRepository(ShareItDbContext context, IMapper mapper) : base(context)
        {
            _mapper = mapper;
        }

        public async Task<List<Favorite>> GetFavoritesByUserIdAsync(Guid userId)
        {
            return await _context.Favorites
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<FavoriteWithProductDto>> GetFavoritesWithProductDetailsAsync(Guid userId)
        {
            // Query favorites với product details và map sang DTO
            var favorites = await _context.Favorites
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Include(f => f.Product)
                    .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
                .Include(f => f.Product.Category)
                .ToListAsync();

            // Map sang DTO
            return favorites.Select(f => new FavoriteWithProductDto
            {
                UserId = f.UserId,
                ProductId = f.ProductId,
                CreatedAt = f.CreatedAt,
                Product = _mapper.Map<ProductDTO>(f.Product)
            }).ToList();
        }

        public async Task<bool> IsFavoriteAsync(Guid userId, Guid productId)
        {
            return await _context.Favorites
                .AsNoTracking()
                .AnyAsync(f => f.UserId == userId && f.ProductId == productId);
        }

        public async Task AddFavoriteAsync(Favorite favorite)
        {
            _context.Favorites.Add(favorite);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFavoriteAsync(Guid userId, Guid productId)
        {
            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
            }
        }
    }
}
