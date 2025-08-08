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

            cartDto.Items = cart.Items.Select(ci => _mapper.Map<CartItemDto>(ci)).ToList();

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
            // Đảm bảo CartItem được include Cart để kiểm tra CustomerId
            var cartItem = await _cartRepository.GetCartItemByIdAsync(cartItemId);

            if (cartItem == null || cartItem.Cart.CustomerId != customerId)
            {
                return false; // Không tìm thấy mục giỏ hàng hoặc không thuộc về người dùng hiện tại
            }

            // Lấy giá trị hiện tại (nếu cần dùng cho logic phức tạp sau này)
            int currentQuantity = cartItem.Quantity;
            int currentRentalDays = cartItem.RentalDays;

            // Cập nhật Quantity nếu updateDto.Quantity có giá trị
            if (updateDto.Quantity.HasValue) // Chỉ kiểm tra HasValue, không kiểm tra Value >= 1 ở đây
            {
                if (updateDto.Quantity.Value >= 1)
                {
                    cartItem.Quantity = updateDto.Quantity.Value;
                }
                else // Nếu Quantity được gửi với giá trị < 1, coi như muốn xóa item
                {
                    return await RemoveCartItemAsync(customerId, cartItemId); // Xóa item nếu Quantity về 0
                }
            }
            // Cập nhật StartDate nếu có
            if (updateDto.StartDate.HasValue)
            {
                var newStartDate = updateDto.StartDate.Value.Date;
                if (newStartDate < DateTime.UtcNow.Date)
                {
                    throw new ArgumentException("Start Date cannot be in the past.");
                }
                cartItem.StartDate = newStartDate;
                // Tính lại EndDate dựa trên RentalDays hiện tại
                cartItem.EndDate = cartItem.StartDate.AddDays(cartItem.RentalDays);
            }

            if (updateDto.RentalDays.HasValue)
            {
                if (updateDto.RentalDays.Value >= 1)
                {
                    cartItem.RentalDays = updateDto.RentalDays.Value;
                    // Tính toán lại EndDate khi RentalDays thay đổi
                    cartItem.EndDate = cartItem.StartDate.AddDays(cartItem.RentalDays);
                }
                else
                {
                    
                    throw new ArgumentException("Rental Days must be at least 1.");
                   
                }
            }
            if (!updateDto.Quantity.HasValue && !updateDto.RentalDays.HasValue && !updateDto.StartDate.HasValue)
            {
                return false; // Không có dữ liệu nào để cập nhật
            }


            await _cartRepository.UpdateCartItemAsync(cartItem);
            return true; // Luôn trả về true nếu không có lỗi và đã có ít nhất 1 giá trị được gửi để cập nhật (hoặc xóa)
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
