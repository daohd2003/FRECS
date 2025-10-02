using AutoMapper;
using BusinessObject.DTOs.CartDto;
using BusinessObject.Models;
using BusinessObject.Utilities;
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

            // Get cart items with correct pricing based on transaction type
            var cartItemDtos = new List<CartItemDto>();
            foreach (var cartItem in cart.Items)
            {
                var cartItemDto = _mapper.Map<CartItemDto>(cartItem);
                
                // Set price and total based on transaction type - AutoMapper will handle this now
                // but we'll keep this for any additional processing if needed
                
                cartItemDtos.Add(cartItemDto);
            }

            cartDto.Items = cartItemDtos;

            // Sửa từ GrandTotal thành TotalAmount
            cartDto.TotalAmount = cartDto.Items.Sum(item => item.TotalItemPrice);
            
            // Calculate total deposit amount for all rental items
            cartDto.TotalDepositAmount = cartDto.Items
                .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                .Sum(item => item.TotalDepositAmount);

            return cartDto;
        }

        public async Task<bool> AddProductToCartAsync(Guid customerId, CartAddRequestDto cartAddRequestDto)
        {
            var product = await _productRepository.GetByIdAsync(cartAddRequestDto.ProductId);
            if (product == null)
            {
                throw new ArgumentException("Product not found.");
            }

            // Validate that user cannot add their own product to cart
            if (product.ProviderId == customerId)
            {
                throw new ArgumentException("You cannot add your own product to cart.");
            }

            // Validate transaction type availability
            if (cartAddRequestDto.TransactionType == BusinessObject.Enums.TransactionType.purchase)
            {
                if (product.PurchaseStatus != BusinessObject.Enums.PurchaseStatus.Available || 
                    product.PurchaseQuantity <= 0)
                {
                    throw new ArgumentException("Product is not available for purchase.");
                }
            }
            else // Rental
            {
                if (product.RentalStatus != BusinessObject.Enums.RentalStatus.Available || 
                    product.RentalQuantity <= 0)
                {
                    throw new ArgumentException("Product is not available for rental.");
                }
            }

            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId, CreatedAt = DateTimeHelper.GetVietnamTime() };
                await _cartRepository.CreateCartAsync(cart);
            }

            // Handle rental-specific logic
            if (cartAddRequestDto.TransactionType == BusinessObject.Enums.TransactionType.rental)
            {
                // Defaults if not provided from Product Detail page
                int rentalDays = cartAddRequestDto.RentalDays.HasValue && cartAddRequestDto.RentalDays.Value >= 1
                    ? cartAddRequestDto.RentalDays.Value
                    : 1;
                DateTime startDate = (cartAddRequestDto.StartDate?.Date ?? DateTime.UtcNow.Date.AddDays(1));
                if (startDate < DateTime.UtcNow.Date)
                {
                    startDate = DateTime.UtcNow.Date.AddDays(1);
                }

                // Find existing rental item with same ProductId, Size, StartDate, RentalDays
                var existingCartItem = cart.Items.FirstOrDefault(ci =>
                    ci.ProductId == cartAddRequestDto.ProductId &&
                    ci.TransactionType == BusinessObject.Enums.TransactionType.rental &&
                    ci.StartDate.HasValue && ci.StartDate.Value.Date == startDate && 
                    ci.RentalDays == rentalDays);

                if (existingCartItem != null)
                {
                    // Validate stock before adding to existing quantity
                    int newTotalQuantity = existingCartItem.Quantity + cartAddRequestDto.Quantity;
                    if (product.RentalQuantity > 0 && newTotalQuantity > product.RentalQuantity)
                    {
                        throw new InvalidOperationException($"Cannot add more items. Only {product.RentalQuantity} units available for rental, but you're trying to add {newTotalQuantity} units in total.");
                    }
                    
                    existingCartItem.Quantity += cartAddRequestDto.Quantity;
                    existingCartItem.EndDate = existingCartItem.StartDate.Value.AddDays(existingCartItem.RentalDays.Value);
                    await _cartRepository.UpdateCartItemAsync(existingCartItem);
                }
                else
                {
                    // Validate stock for new cart item
                    if (product.RentalQuantity > 0 && cartAddRequestDto.Quantity > product.RentalQuantity)
                    {
                        throw new InvalidOperationException($"Cannot add to cart. Only {product.RentalQuantity} units available for rental, but you're trying to add {cartAddRequestDto.Quantity} units.");
                    }
                    
                    var newCartItem = _mapper.Map<CartItem>(cartAddRequestDto);
                    newCartItem.CartId = cart.Id;
                    newCartItem.Id = Guid.NewGuid();
                    // Apply rental defaults
                    newCartItem.StartDate = startDate;
                    newCartItem.RentalDays = rentalDays;
                    newCartItem.EndDate = newCartItem.StartDate.Value.AddDays(newCartItem.RentalDays.Value);

                    await _cartRepository.AddCartItemAsync(newCartItem);
                }
            }
            else // Purchase
            {
                // Find existing purchase item with same ProductId and Size
                var existingCartItem = cart.Items.FirstOrDefault(ci =>
                    ci.ProductId == cartAddRequestDto.ProductId &&
                    ci.TransactionType == BusinessObject.Enums.TransactionType.purchase);

                if (existingCartItem != null)
                {
                    // Validate stock before adding to existing quantity
                    int newTotalQuantity = existingCartItem.Quantity + cartAddRequestDto.Quantity;
                    if (product.PurchaseQuantity > 0 && newTotalQuantity > product.PurchaseQuantity)
                    {
                        throw new InvalidOperationException($"Cannot add more items. Only {product.PurchaseQuantity} units available for purchase, but you're trying to add {newTotalQuantity} units in total.");
                    }
                    
                    existingCartItem.Quantity += cartAddRequestDto.Quantity;
                    await _cartRepository.UpdateCartItemAsync(existingCartItem);
                }
                else
                {
                    // Validate stock for new cart item
                    if (product.PurchaseQuantity > 0 && cartAddRequestDto.Quantity > product.PurchaseQuantity)
                    {
                        throw new InvalidOperationException($"Cannot add to cart. Only {product.PurchaseQuantity} units available for purchase, but you're trying to add {cartAddRequestDto.Quantity} units.");
                    }
                    
                    var newCartItem = _mapper.Map<CartItem>(cartAddRequestDto);
                    newCartItem.CartId = cart.Id;
                    newCartItem.Id = Guid.NewGuid();
                    // For purchase: no rental dates needed
                    newCartItem.StartDate = null;
                    newCartItem.RentalDays = null;
                    newCartItem.EndDate = null;

                    await _cartRepository.AddCartItemAsync(newCartItem);
                }
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
                    // Validate stock quantity before updating
                    var product = await _productRepository.GetByIdAsync(targetItem.ProductId);
                    if (product != null)
                    {
                        int availableStock = targetItem.TransactionType == BusinessObject.Enums.TransactionType.purchase
                            ? product.PurchaseQuantity
                            : product.RentalQuantity;
                            
                        // Only validate if there's actual stock data (> 0)
                        if (availableStock > 0 && updateDto.Quantity.Value > availableStock)
                        {
                            throw new InvalidOperationException($"Quantity exceeds available stock. Only {availableStock} units available.");
                        }
                    }
                    
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
                    item.EndDate = item.StartDate.HasValue && item.RentalDays.HasValue 
                        ? item.StartDate.Value.AddDays(item.RentalDays.Value) 
                        : null;
                }

                if (updateDto.RentalDays.HasValue)
                {
                    if (updateDto.RentalDays.Value < 1)
                    {
                        throw new ArgumentException("Rental Days must be at least 1.");
                    }
                    item.RentalDays = updateDto.RentalDays.Value;
                    item.EndDate = item.StartDate.HasValue && item.RentalDays.HasValue 
                        ? item.StartDate.Value.AddDays(item.RentalDays.Value) 
                        : null;
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
