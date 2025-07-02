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

            cartDto.Items = cart.Items.Select(ci => _mapper.Map<CartItemDto>(ci)).ToList();

            // Sửa từ GrandTotal thành TotalAmount
            cartDto.TotalAmount = cartDto.Items.Sum(item => item.TotalItemPrice);

            return cartDto;
        }

        public async Task<bool> AddProductToCartAsync(Guid customerId, CartAddRequestDto cartItemDto)
        {
            var product = await _productRepository.GetByIdAsync(cartItemDto.ProductId);
            if (product == null) return false;

            // Lấy hoặc tạo Cart
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId };
                await _cartRepository.CreateCartAsync(cart);
            }

            // Kiểm tra CartItem đã tồn tại chưa
            var existingCartItem = await _cartRepository
            .GetCartItemByProductIdAndCartIdAsync(cart.Id, cartItemDto.ProductId);

            if (existingCartItem != null)
            {
                // Cập nhật số lượng và số ngày thuê
                existingCartItem.Quantity += cartItemDto.Quantity;
                existingCartItem.RentalDays = Math.Max(existingCartItem.RentalDays, cartItemDto.RentalDays); // lấy số ngày lớn hơn
                await _cartRepository.UpdateCartItemAsync(existingCartItem);
            }
            else
            {
                // Dùng AutoMapper nếu đã cấu hình
                var newCartItem = _mapper.Map<CartItem>(cartItemDto);
                newCartItem.CartId = cart.Id;
                await _cartRepository.AddCartItemAsync(newCartItem);
            }

            return true;
        }

        public async Task<bool> UpdateCartItemAsync(Guid customerId, Guid cartItemId, CartUpdateRequestDto updateDto)
        {
            var cartItem = await _cartRepository.GetCartItemByIdAsync(cartItemId);

            if (cartItem == null || cartItem.Cart.CustomerId != customerId)
            {
                return false; // Cart item not found or does not belong to the user
            }

            cartItem.RentalDays = updateDto.RentalDays;
            cartItem.Quantity = updateDto.Quantity;
            await _cartRepository.UpdateCartItemAsync(cartItem);
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
            if (cart == null || cart.Items == null)
            {
                return 0;
            }
            return cart.Items.Count;
        }
    }
}
