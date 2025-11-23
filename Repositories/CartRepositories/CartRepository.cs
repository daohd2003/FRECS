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

        /// <summary>
        /// Lấy giỏ hàng của customer với đầy đủ thông tin (Items, Product, Images)
        /// Eager loading để tránh N+1 query problem
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <returns>Cart entity với Items, null nếu chưa có giỏ hàng</returns>
        public async Task<Cart> GetCartByCustomerIdAsync(Guid customerId)
        {
            return await _context.Carts
                .Include(c => c.Items) // Eager loading CartItems
                    .ThenInclude(ci => ci.Product) // Eager loading Product của từng item
                    .ThenInclude(p => p.Images) // Eager loading Images của Product
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        /// <summary>
        /// Tạo giỏ hàng mới cho customer
        /// Thường được gọi khi customer thêm sản phẩm đầu tiên vào giỏ
        /// </summary>
        /// <param name="cart">Cart entity cần tạo</param>
        public async Task CreateCartAsync(Cart cart)
        {
            await _context.Carts.AddAsync(cart);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Thêm item mới vào giỏ hàng
        /// Được gọi khi customer thêm sản phẩm vào giỏ
        /// </summary>
        /// <param name="cartItem">CartItem entity cần thêm</param>
        public async Task AddCartItemAsync(CartItem cartItem)
        {
            await _context.CartItems.AddAsync(cartItem);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Cập nhật cart item (thường là cập nhật số lượng, ngày thuê)
        /// Được gọi khi customer thay đổi số lượng hoặc thời gian thuê
        /// </summary>
        /// <param name="cartItem">CartItem entity đã được cập nhật</param>
        public async Task UpdateCartItemAsync(CartItem cartItem)
        {
            _context.CartItems.Update(cartItem);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa item khỏi giỏ hàng
        /// Được gọi khi customer remove sản phẩm khỏi giỏ
        /// </summary>
        /// <param name="cartItem">CartItem entity cần xóa</param>
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
