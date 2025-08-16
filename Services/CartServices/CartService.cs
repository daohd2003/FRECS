using AutoMapper;
using BusinessObject.DTOs.CartDto;
using BusinessObject.Models;
using Repositories.CartRepositories;
using Repositories.ProductRepositories;
using Repositories.UserRepositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Services.CartServices
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IProductRepository _productRepository; // Assume you have a ProductRepository
        private readonly IUserRepository _userRepository; // Assume you have a UserRepository
        private readonly IMapper _mapper;
        public CartService(ICartRepository cartRepository, IProductRepository productRepository, IUserRepository userRepository, IMapper mapper)
        {
            _cartRepository = cartRepository;
            _productRepository = productRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<CartDto> GetUserCartAsync(Guid customerId)
        {
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                return null;
            }

            var cartDto = _mapper.Map<CartDto>(cart);

            // Get cart items with discounted prices
            var cartItemDtos = new List<CartItemDto>();
            foreach (var cartItem in cart.Items)
            {
                var cartItemDto = _mapper.Map<CartItemDto>(cartItem);
                
                // Use original price
                cartItemDto.PricePerUnit = cartItem.Product.PricePerDay;
                cartItemDto.TotalItemPrice = cartItem.Product.PricePerDay * cartItem.Quantity * cartItem.RentalDays;
                
                cartItemDtos.Add(cartItemDto);
            }

            cartDto.Items = cartItemDtos;

            // Sửa từ GrandTotal thành TotalAmount
            cartDto.TotalAmount = cartDto.Items.Sum(item => item.TotalItemPrice);

            return cartDto;
        }

        public async Task<bool> AddProductToCartAsync(Guid customerId, CartAddRequestDto cartAddRequestDto)
        {
            var product = await _productRepository.GetByIdAsync(cartAddRequestDto.ProductId);
            if (product == null)
            {
                throw new ArgumentException("Product not found.");
            }

            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId, CreatedAt = DateTime.UtcNow };
                await _cartRepository.CreateCartAsync(cart);
            }

            // Defaults if not provided from Product Detail page
            int rentalDays = cartAddRequestDto.RentalDays.HasValue && cartAddRequestDto.RentalDays.Value >= 1
                ? cartAddRequestDto.RentalDays.Value
                : 1;
            DateTime startDate = (cartAddRequestDto.StartDate?.Date ?? DateTime.UtcNow.Date.AddDays(1));
            if (startDate < DateTime.UtcNow.Date)
            {
                startDate = DateTime.UtcNow.Date.AddDays(1);
            }

            // Lấy CartItem hiện có (nếu có cùng ProductId, StartDate, RentalDays)
            var existingCartItem = cart.Items.FirstOrDefault(ci =>
                ci.ProductId == cartAddRequestDto.ProductId &&
                ci.StartDate.Date == startDate && // So sánh ngày
                ci.RentalDays == rentalDays);// So sánh số ngày thuê)

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += cartAddRequestDto.Quantity; // Cộng thêm số lượng
                existingCartItem.EndDate = existingCartItem.StartDate.AddDays(existingCartItem.RentalDays); // Cập nhật lại EndDate
                await _cartRepository.UpdateCartItemAsync(existingCartItem);
            }
            else
            {
                var newCartItem = _mapper.Map<CartItem>(cartAddRequestDto); // Ánh xạ từ DTO
                newCartItem.CartId = cart.Id;
                newCartItem.Id = Guid.NewGuid(); // Gán Id mới
                // Apply defaults
                newCartItem.StartDate = startDate;
                newCartItem.RentalDays = rentalDays;
                newCartItem.EndDate = newCartItem.StartDate.AddDays(newCartItem.RentalDays);

                // EndDate đã được tính toán trong Mapper Profile khi map từ CartAddRequestDto
                await _cartRepository.AddCartItemAsync(newCartItem);
            }

            return true;
        }

        public async Task<bool> UpdateCartItemAsync(Guid customerId, Guid cartItemId, CartUpdateRequestDto updateDto)
        {
            // Path 1: Only Quantity is provided -> update a SINGLE item (do not unify)
            if (updateDto.Quantity.HasValue && !updateDto.RentalDays.HasValue && !updateDto.StartDate.HasValue)
            {
                var targetItem = await _cartRepository.GetCartItemByIdAsync(cartItemId);
                if (targetItem == null || targetItem.Cart.CustomerId != customerId)
                {
                    return false;
                }
                if (updateDto.Quantity.Value >= 1)
                {
                    targetItem.Quantity = updateDto.Quantity.Value;
                    await _cartRepository.UpdateCartItemAsync(targetItem);
                    return true;
                }
                else
                {
                    await _cartRepository.DeleteCartItemAsync(targetItem);
                    return true;
                }
            }

            // Path 2: StartDate and/or RentalDays provided -> apply to ALL items in cart
            if (!updateDto.RentalDays.HasValue && !updateDto.StartDate.HasValue)
            {
                return false; // nothing to update
            }

            var cartItems = await _cartRepository.GetCartItemsForCustomerQuery(customerId).ToListAsync();
            if (!cartItems.Any()) return false;

            foreach (var item in cartItems)
            {
                if (updateDto.StartDate.HasValue)
                {
                    var newStart = updateDto.StartDate.Value.Date;
                    if (newStart < DateTime.UtcNow.Date)
                    {
                        throw new ArgumentException("Start Date cannot be in the past.");
                    }
                    item.StartDate = newStart;
                    item.EndDate = item.StartDate.AddDays(item.RentalDays);
                }

                if (updateDto.RentalDays.HasValue)
                {
                    if (updateDto.RentalDays.Value < 1)
                    {
                        throw new ArgumentException("Rental Days must be at least 1.");
                    }
                    item.RentalDays = updateDto.RentalDays.Value;
                    item.EndDate = item.StartDate.AddDays(item.RentalDays);
                }

                await _cartRepository.UpdateCartItemAsync(item);
            }

            return true;
        }

        public async Task<bool> RemoveCartItemAsync(Guid customerId, Guid cartItemId)
        {
            var cartItem = await _cartRepository.GetCartItemByIdAsync(cartItemId);

            if (cartItem == null || cartItem.Cart.CustomerId != customerId)
            {
                return false; // Cart item not found or does not belong to the user
            }

            await _cartRepository.DeleteCartItemAsync(cartItem);
            return true;
        }

        public async Task<int> GetCartItemCountAsync(Guid customerId)
        {
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            // Trả về tổng số lượng sản phẩm trong giỏ hàng (sum of Quantity), không phải số dòng
            return cart?.Items?.Sum(ci => ci.Quantity) ?? 0;
            // Nếu bạn muốn số dòng (số loại sản phẩm khác nhau): return cart?.Items?.Count ?? 0;
        }
    }
}
