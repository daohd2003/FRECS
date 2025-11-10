using AutoMapper;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Repositories.FavoriteRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.FavoriteServices
{
    public class FavoriteService : IFavoriteService
    {
        private readonly IFavoriteRepository _favoriteRepository;
        private readonly IMapper _mapper;

        public FavoriteService(IFavoriteRepository favoriteRepository, IMapper mapper)
        {
            _favoriteRepository = favoriteRepository;
            _mapper = mapper;
        }

        public async Task<List<Favorite>> GetFavoritesByUserIdAsync(Guid userId)
        {
            return await _favoriteRepository.GetFavoritesByUserIdAsync(userId);
        }

        public async Task<List<FavoriteWithProductDto>> GetFavoritesWithProductDetailsAsync(Guid userId)
        {
            // Get favorites with product details from repository
            var favorites = await _favoriteRepository.GetFavoritesWithProductDetailsAsync(userId);

            // Map to DTO in service layer
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
            return await _favoriteRepository.IsFavoriteAsync(userId, productId);
        }

        public async Task AddFavoriteAsync(Favorite favorite)
        {
            await _favoriteRepository.AddFavoriteAsync(favorite);
        }

        public async Task RemoveFavoriteAsync(Guid userId, Guid productId)
        {
            await _favoriteRepository.RemoveFavoriteAsync(userId, productId);
        }
    }
}
