using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.FavoriteRepositories
{
    public interface IFavoriteRepository : IRepository<Favorite>
    {
        Task<List<Favorite>> GetFavoritesByUserIdAsync(Guid userId);
        Task<List<Favorite>> GetFavoritesWithProductDetailsAsync(Guid userId);
        Task<bool> IsFavoriteAsync(Guid userId, Guid productId);
        Task AddFavoriteAsync(Favorite favorite);
        Task RemoveFavoriteAsync(Guid userId, Guid productId);
    }
}
