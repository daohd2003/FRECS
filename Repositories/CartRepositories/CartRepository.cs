using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.CartRepositories
{
    public class CartRepository : ICartRepository
    {
        private readonly ShareItDbContext _context;

        public CartRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<Cart> GetCartByCustomerIdAsync(Guid customerId)
        {
            return await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.Images)// Include product details for cart items
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public async Task CreateCartAsync(Cart cart)
        {
            await _context.Carts.AddAsync(cart);
            await _context.SaveChangesAsync();
        }

        public async Task AddCartItemAsync(CartItem cartItem)
        {
            await _context.CartItems.AddAsync(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCartItemAsync(CartItem cartItem)
        {
            _context.CartItems.Update(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCartItemAsync(CartItem cartItem)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task<CartItem> GetCartItemByIdAsync(Guid cartItemId)
        {
            return await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)// Include product details
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);
        }

        public async Task<CartItem> GetCartItemByProductIdAndCartIdAsync(Guid cartId, Guid productId)
        {
            return await _context.CartItems
                .Include(ci => ci.Product) // Include product details
                .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId);
        }

        public IQueryable<CartItem> GetCartItemsForCustomerQuery(Guid customerId)
        {
            return _context.CartItems
                .Where(ci => ci.Cart.CustomerId == customerId)
                .Include(ci => ci.Product); // Include product details
        }
    }
}
