using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.FavoriteServices
{
    public interface IFavoriteService
    {
        Task<List<Favorite>> GetFavoritesByUserIdAsync(Guid userId);
        Task<List<FavoriteWithProductDto>> GetFavoritesWithProductDetailsAsync(Guid userId);
        Task<bool> IsFavoriteAsync(Guid userId, Guid productId);
        Task AddFavoriteAsync(Favorite favorite);
        Task RemoveFavoriteAsync(Guid userId, Guid productId);
    }
}
