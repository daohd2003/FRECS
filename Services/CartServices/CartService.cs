using AutoMapper;
using BusinessObject.DTOs.CartDto;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.CartRepositories;
using Repositories.ProductRepositories;
using Repositories.UserRepositories;
using Repositories.OrderRepositories;

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
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;
        public CartService(ICartRepository cartRepository, IProductRepository productRepository, IUserRepository userRepository, IOrderRepository orderRepository, IMapper mapper)
        {
            _cartRepository = cartRepository;
            _productRepository = productRepository;
            _userRepository = userRepository;
            _orderRepository = orderRepository;
            _mapper = mapper;
        }

        /// <summary>
        /// Lấy giỏ hàng của người dùng với đầy đủ thông tin sản phẩm và tính toán giá
        /// Tính tổng tiền hàng và tổng tiền cọc (cho đơn thuê)
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <returns>CartDto chứa danh sách items và tổng tiền, null nếu chưa có giỏ hàng</returns>
        public async Task<CartDto> GetUserCartAsync(Guid customerId)
        {
            // Bước 1: Lấy giỏ hàng từ database (đã include Items, Product)
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                return null; // Chưa có giỏ hàng
            }

            // Bước 2: Chuyển đổi Entity sang DTO bằng AutoMapper
            var cartDto = _mapper.Map<CartDto>(cart);

            // Bước 3: Xử lý từng cart item để tính giá chính xác
            var cartItemDtos = new List<CartItemDto>();
            foreach (var cartItem in cart.Items)
            {
                var cartItemDto = _mapper.Map<CartItemDto>(cartItem);
                
                // AutoMapper đã tự động tính giá dựa trên transaction type
                // - Thuê: giá/ngày × số ngày × số lượng
                // - Mua: giá mua × số lượng
                
                cartItemDtos.Add(cartItemDto);
            }

            cartDto.Items = cartItemDtos;

            // Bước 4: Tính tổng tiền hàng (không bao gồm cọc)
            cartDto.TotalAmount = cartDto.Items.Sum(item => item.TotalItemPrice);
            
            // Bước 5: Tính tổng tiền cọc (chỉ cho đơn thuê)
            cartDto.TotalDepositAmount = cartDto.Items
                .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                .Sum(item => item.TotalDepositAmount);

            return cartDto;
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng với các validation về số lượng, trạng thái, và quyền sở hữu
        /// Xử lý logic khác nhau cho thuê (có ngày bắt đầu, số ngày) và mua (không có)
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <param name="cartAddRequestDto">Thông tin sản phẩm cần thêm (ProductId, Quantity, TransactionType, RentalDays, StartDate)</param>
        /// <returns>true nếu thêm thành công</returns>
        /// <exception cref="ArgumentException">Sản phẩm không tồn tại, không khả dụng, hoặc là sản phẩm của chính user</exception>
        /// <exception cref="InvalidOperationException">Số lượng vượt quá tồn kho</exception>
        public async Task<bool> AddProductToCartAsync(Guid customerId, CartAddRequestDto cartAddRequestDto)
        {
            // Bước 1: Kiểm tra sản phẩm có tồn tại không
            var product = await _productRepository.GetByIdAsync(cartAddRequestDto.ProductId);
            if (product == null)
            {
                throw new ArgumentException("Product not found.");
            }

            // Bước 2: Validate user không thể thêm sản phẩm của chính mình vào giỏ
            // (Provider không thể mua/thuê sản phẩm của chính mình)
            if (product.ProviderId == customerId)
            {
                throw new ArgumentException("You cannot add your own product to cart.");
            }

            // Bước 3: Validate sản phẩm có khả dụng cho loại giao dịch không
            if (cartAddRequestDto.TransactionType == BusinessObject.Enums.TransactionType.purchase)
            {
                // Kiểm tra trạng thái mua và số lượng
                if (product.PurchaseStatus != BusinessObject.Enums.PurchaseStatus.Available || 
                    product.PurchaseQuantity <= 0)
                {
                    throw new ArgumentException("Product is not available for purchase.");
                }
            }
            else // Rental
            {
                // Kiểm tra trạng thái thuê và số lượng
                if (product.RentalStatus != BusinessObject.Enums.RentalStatus.Available || 
                    product.RentalQuantity <= 0)
                {
                    throw new ArgumentException("Product is not available for rental.");
                }
            }

            // Bước 4: Lấy hoặc tạo giỏ hàng cho customer
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                // Tạo giỏ hàng mới nếu chưa có
                cart = new Cart { CustomerId = customerId, CreatedAt = DateTimeHelper.GetVietnamTime() };
                await _cartRepository.CreateCartAsync(cart);
            }

            // Bước 5: Xử lý logic riêng cho THUÊ
            if (cartAddRequestDto.TransactionType == BusinessObject.Enums.TransactionType.rental)
            {
                // Xác định số ngày thuê (mặc định 1 ngày nếu không có)
                int rentalDays = cartAddRequestDto.RentalDays.HasValue && cartAddRequestDto.RentalDays.Value >= 1
                    ? cartAddRequestDto.RentalDays.Value
                    : 1;
                
                // Validate số ngày thuê tối đa 7 ngày
                if (rentalDays > 7)
                {
                    throw new ArgumentException("Rental Days cannot exceed 7 days.");
                }
                
                // Xác định ngày bắt đầu thuê (mặc định ngày mai nếu không có)
                var vietnamNow = DateTimeHelper.GetVietnamTime();
                DateTime startDate = (cartAddRequestDto.StartDate?.Date ?? vietnamNow.Date.AddDays(1));
                if (startDate < vietnamNow.Date)
                {
                    startDate = vietnamNow.Date.AddDays(1); // Không cho thuê ngày quá khứ
                }

                // Tìm cart item đã tồn tại với cùng ProductId, StartDate, RentalDays
                // Nếu có → Tăng số lượng
                // Nếu không → Tạo mới
                var existingCartItem = cart.Items.FirstOrDefault(ci =>
                    ci.ProductId == cartAddRequestDto.ProductId &&
                    ci.TransactionType == BusinessObject.Enums.TransactionType.rental &&
                    ci.StartDate.HasValue && ci.StartDate.Value.Date == startDate && 
                    ci.RentalDays == rentalDays);

                if (existingCartItem != null)
                {
                    // Đã có item → Tăng số lượng
                    // Validate số lượng mới không vượt quá tồn kho
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
                    // Chưa có item → Tạo mới
                    // Validate số lượng không vượt quá tồn kho
                    if (product.RentalQuantity > 0 && cartAddRequestDto.Quantity > product.RentalQuantity)
                    {
                        throw new InvalidOperationException($"Cannot add to cart. Only {product.RentalQuantity} units available for rental, but you're trying to add {cartAddRequestDto.Quantity} units.");
                    }
                    
                    var newCartItem = _mapper.Map<CartItem>(cartAddRequestDto);
                    newCartItem.CartId = cart.Id;
                    newCartItem.Id = Guid.NewGuid();
                    // Gán thông tin thuê
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

            // Validate total cost after adding product
            await ValidateCartTotalCostAsync(customerId);

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
                    
                    // Store old quantity for rollback if needed
                    int oldQuantity = targetItem.Quantity;
                    
                    // Temporarily set new quantity for validation
                    targetItem.Quantity = updateDto.Quantity.Value;
                    
                    try
                    {
                        // Validate total cost BEFORE committing to database
                        await ValidateCartTotalCostAsync(customerId);
                        
                        // If validation passes, save to database
                        await _cartRepository.UpdateCartItemAsync(targetItem);
                        
                        return true;
                    }
                    catch
                    {
                        // Rollback quantity if validation fails
                        targetItem.Quantity = oldQuantity;
                        throw; // Re-throw the validation exception
                    }
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

            // Store old values for rollback if validation fails
            var oldValues = cartItems.Select(item => new 
            { 
                Item = item, 
                OldStartDate = item.StartDate, 
                OldRentalDays = item.RentalDays,
                OldEndDate = item.EndDate
            }).ToList();

            try
            {
                // First, update all items in memory (not saved to DB yet)
                foreach (var item in cartItems)
                {
                    if (updateDto.StartDate.HasValue)
                    {
                        var newStart = updateDto.StartDate.Value.Date;
                        if (newStart < DateTimeHelper.GetVietnamTime().Date)
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
                        if (updateDto.RentalDays.Value > 7)
                        {
                            throw new ArgumentException("Rental Days cannot exceed 7 days.");
                        }
                        item.RentalDays = updateDto.RentalDays.Value;
                        item.EndDate = item.StartDate.HasValue && item.RentalDays.HasValue 
                            ? item.StartDate.Value.AddDays(item.RentalDays.Value) 
                            : null;
                    }
                }

                // Validate total cost BEFORE saving to database
                await ValidateCartTotalCostAsync(customerId);

                // If validation passes, save all changes to database
                foreach (var item in cartItems)
                {
                    await _cartRepository.UpdateCartItemAsync(item);
                }
            }
            catch
            {
                // Rollback all changes if validation fails
                foreach (var old in oldValues)
                {
                    old.Item.StartDate = old.OldStartDate;
                    old.Item.RentalDays = old.OldRentalDays;
                    old.Item.EndDate = old.OldEndDate;
                }
                throw; // Re-throw the validation exception
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

        /// <summary>
        /// Xóa toàn bộ sản phẩm trong giỏ hàng
        /// Dùng khi: checkout thành công, user muốn làm mới giỏ hàng, hoặc rent again
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <returns>true nếu xóa thành công hoặc giỏ hàng đã rỗng</returns>
        public async Task<bool> ClearCartAsync(Guid customerId)
        {
            // Lấy giỏ hàng hiện tại
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                return true; // Giỏ hàng đã rỗng hoặc chưa tồn tại
            }

            // Lấy tất cả cart items và xóa từng item
            var cartItems = await _cartRepository.GetCartItemsForCustomerQuery(customerId).ToListAsync();
            foreach (var item in cartItems)
            {
                await _cartRepository.DeleteCartItemAsync(item);
            }

            return true;
        }

        public async Task<(bool success, int addedCount, int issuesCount)> AddOrderItemsToCartAsync(Guid customerId, Guid orderId, bool preserveDates = false)
        {
            // Get order with items
            var order = await _orderRepository.GetOrderWithItemsAsync(orderId);
            if (order == null)
            {
                throw new ArgumentException("Order not found.");
            }

            if (order.CustomerId != customerId)
            {
                throw new UnauthorizedAccessException("You are not authorized to access this order.");
            }

            // Clear current cart
            await ClearCartAsync(customerId);

            // Get or create cart for customer
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null)
            {
                cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CreatedAt = DateTimeHelper.GetVietnamTime()
                };
                await _cartRepository.CreateCartAsync(cart);
            }

            // Track items that were added or skipped
            int addedCount = 0;
            int skippedCount = 0;
            int adjustedCount = 0; // Track items with adjusted quantity

            // Add each order item to cart
            foreach (var orderItem in order.Items)
            {
                var product = await _productRepository.GetByIdAsync(orderItem.ProductId);
                if (product == null)
                {
                    skippedCount++;
                    continue; // Skip if product not found
                }

                // Check if product is still available
                if (product.AvailabilityStatus != BusinessObject.Enums.AvailabilityStatus.available)
                {
                    skippedCount++;
                    continue; // Skip unavailable products
                }

                // Check quantity based on transaction type
                int availableQuantity = orderItem.TransactionType == BusinessObject.Enums.TransactionType.rental
                    ? product.RentalQuantity
                    : product.PurchaseQuantity;

                if (availableQuantity <= 0)
                {
                    skippedCount++;
                    continue; // Skip if no quantity available
                }

                // Adjust quantity if requested quantity exceeds available quantity
                int quantityToAdd = Math.Min(orderItem.Quantity, availableQuantity);
                
                // Track if quantity was adjusted
                if (quantityToAdd < orderItem.Quantity)
                {
                    adjustedCount++;
                }

                // Determine start date based on preserveDates flag
                DateTime? startDate = null;
                DateTime? endDate = null;
                
                if (orderItem.TransactionType == BusinessObject.Enums.TransactionType.rental)
                {
                    if (preserveDates && order.RentalStart.HasValue)
                    {
                        // Pay Now: Use original order's rental dates
                        startDate = order.RentalStart.Value.Date;
                        if (orderItem.RentalDays.HasValue)
                        {
                            endDate = startDate.Value.AddDays(orderItem.RentalDays.Value);
                        }
                    }
                    else
                    {
                        // Rent Again: Set start date to tomorrow (user can adjust in cart)
                        startDate = DateTimeHelper.GetVietnamTime().Date.AddDays(1);
                        if (orderItem.RentalDays.HasValue)
                        {
                            endDate = startDate.Value.AddDays(orderItem.RentalDays.Value);
                        }
                    }
                }

                var cartItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    ProductId = orderItem.ProductId,
                    Quantity = quantityToAdd,
                    TransactionType = orderItem.TransactionType,
                    RentalDays = orderItem.RentalDays,
                    StartDate = startDate,
                    EndDate = endDate
                };

                await _cartRepository.AddCartItemAsync(cartItem);
                addedCount++;
            }

            // If no items were added, throw exception
            if (addedCount == 0)
            {
                throw new InvalidOperationException("No items could be added to cart. All products are either unavailable or out of stock.");
            }

            // Validate total cost after adding order items to cart
            await ValidateCartTotalCostAsync(customerId);

            // Count skippedCount and adjustedCount together for warning message
            int totalIssues = skippedCount + adjustedCount;
            return (true, addedCount, totalIssues);
        }

        /// <summary>
        /// Validate tổng giá trị giỏ hàng không vượt quá 10,000,000 VND
        /// Tổng bao gồm: giá thuê/mua + tiền cọc (cho đơn thuê)
        /// Mục đích: Giới hạn giá trị đơn hàng để giảm rủi ro gian lận và quản lý thanh toán
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <exception cref="InvalidOperationException">Tổng giá trị vượt quá giới hạn</exception>
        private async Task ValidateCartTotalCostAsync(Guid customerId)
        {
            const decimal MAX_CART_COST = 10_000_000m; // Giới hạn 10 triệu VND
            
            // Lấy giỏ hàng hiện tại
            var cart = await _cartRepository.GetCartByCustomerIdAsync(customerId);
            if (cart == null || !cart.Items.Any())
            {
                return; // Giỏ hàng rỗng → Không cần validate
            }

            // Tính tổng giá trị giỏ hàng
            decimal totalCost = 0m; // Tổng tiền hàng (thuê/mua)
            decimal totalDeposit = 0m; // Tổng tiền cọc (chỉ cho thuê)
            
            foreach (var item in cart.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null) continue;

                if (item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                {
                    // Đơn THUÊ: (giá/ngày × số ngày × số lượng) + (cọc × số lượng)
                    decimal rentalDays = item.RentalDays ?? 1;
                    totalCost += product.PricePerDay * rentalDays * item.Quantity;
                    totalDeposit += product.SecurityDeposit * item.Quantity;
                }
                else
                {
                    // Đơn MUA: giá mua × số lượng (không có cọc)
                    totalCost += product.PurchasePrice * item.Quantity;
                }
            }

            // Tổng giá trị = tiền hàng + tiền cọc
            decimal grandTotal = totalCost + totalDeposit;

            // Kiểm tra vượt quá giới hạn
            if (grandTotal > MAX_CART_COST)
            {
                throw new InvalidOperationException($"Total exceeds maximum allowed amount of {MAX_CART_COST:N0} VND. Please reduce quantity or rental days.");
            }
        }
    }
}
