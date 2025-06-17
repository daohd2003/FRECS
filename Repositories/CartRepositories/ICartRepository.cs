using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.CartRepositories
{
    public interface ICartRepository
    {
        Task<Cart> GetCartByCustomerIdAsync(Guid customerId);
        Task AddCartItemAsync(CartItem cartItem);
        Task UpdateCartItemAsync(CartItem cartItem);
        Task DeleteCartItemAsync(CartItem cartItem);
        Task<CartItem> GetCartItemByIdAsync(Guid cartItemId);
        Task<CartItem> GetCartItemByProductIdAndCartIdAsync(Guid cartId, Guid productId);
        Task CreateCartAsync(Cart cart);
        IQueryable<CartItem> GetCartItemsForCustomerQuery(Guid customerId);
    }
}
