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

        public FavoriteService(IFavoriteRepository favoriteRepository)
        {
            _favoriteRepository = favoriteRepository;
        }

        public async Task<List<Favorite>> GetFavoritesByUserIdAsync(Guid userId)
        {
            return await _favoriteRepository.GetFavoritesByUserIdAsync(userId);
        }

        public async Task<List<FavoriteWithProductDto>> GetFavoritesWithProductDetailsAsync(Guid userId)
        {
            return await _favoriteRepository.GetFavoritesWithProductDetailsAsync(userId);
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
