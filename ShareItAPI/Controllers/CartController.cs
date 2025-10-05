using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.ApiResponses; // Add this using statement
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.CartServices;
using Services.OrderServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [Authorize(Roles = "customer,provider")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;

        public CartController(ICartService cartService, IOrderService orderService)
        {
            _cartService = cartService;
            _orderService = orderService;
        }

        private Guid GetCustomerId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID claim not found.");
            }
            return Guid.Parse(userIdClaim);
        }

        /// <summary>
        /// Retrieve current user's cart
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CartDto>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var customerId = GetCustomerId();
                var cart = await _cartService.GetUserCartAsync(customerId);

                if (cart == null)
                {
                    // If cart is null, return an empty cart with a success message
                    return Ok(new ApiResponse<CartDto>("Cart retrieved successfully (empty cart).", new CartDto { CustomerId = customerId, Items = new System.Collections.Generic.List<CartItemDto>(), TotalAmount = 0 }));
                }

                return Ok(new ApiResponse<CartDto>("Cart retrieved successfully.", cart));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>("Unauthorized access.", null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>($"An unexpected error occurred: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Add product to cart
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<CartDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> AddToCart([FromBody] CartAddRequestDto dto)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.AddProductToCartAsync(customerId, dto);

                if (!result)
                {
                    return BadRequest(new ApiResponse<object>("Failed to add product to cart. Please check product ID and quantity.", null));
                }

                var updatedCart = await _cartService.GetUserCartAsync(customerId);
                // For CreatedAtAction, we usually return the created resource.
                // Here, we return the updated cart as it reflects the addition.
                return CreatedAtAction(nameof(GetCart), new ApiResponse<CartDto>("Product added to cart successfully.", updatedCart));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>("Unauthorized access.", null));
            }
            catch (ArgumentException ex) // Catch validation errors (e.g., product not found, not available)
            {
                return BadRequest(new ApiResponse<object>(ex.Message, null));
            }
            catch (InvalidOperationException ex) // Catch quantity validation errors
            {
                return BadRequest(new ApiResponse<object>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>($"An unexpected error occurred: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Update cart item quantity and duration
        /// </summary>
        [HttpPut("{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] CartUpdateRequestDto dto)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.UpdateCartItemAsync(customerId, itemId, dto);

                if (!result)
                {
                    return NotFound(new ApiResponse<object>("Cart item not found or does not belong to the current user.", null));
                }

                return Ok(new ApiResponse<object>("Cart item updated successfully.", null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>("Unauthorized access.", null));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>($"An unexpected error occurred: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("{itemId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> RemoveFromCart(Guid itemId)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.RemoveCartItemAsync(customerId, itemId);

                if (!result)
                {
                    return NotFound(new ApiResponse<object>("Cart item not found or does not belong to the current user.", null));
                }

                // For 204 No Content, we don't return a body, so ApiResponse is not directly used here for the success case.
                // However, for error cases, we can still use it.
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>("Unauthorized access.", null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>($"An unexpected error occurred: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Initiates the checkout process from the current user's cart, creating an order.
        /// </summary>
        [HttpPost("checkout")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OrderDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto checkoutRequestDto)
        {
            try
            {
                if (!checkoutRequestDto.HasAgreedToPolicies)
                {
                    return BadRequest(new ApiResponse<object>(
                "You must read and agree to the Rental and Sales Policy to proceed with payment.",
                null
            ));
                }

                var customerId = GetCustomerId();
                var createdOrders = await _orderService.CreateOrderFromCartAsync(customerId, checkoutRequestDto);

                return StatusCode(StatusCodes.Status201Created,
                    new ApiResponse<IEnumerable<OrderDto>>("Orders created successfully from cart.", createdOrders));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>("Unauthorized access.", null));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<object>(ex.Message, null));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<object>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>($"An unexpected error occurred: {ex.Message}", null));
            }
        }

        [HttpGet("count")]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> GetCartCount()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized();
            }

            var count = await _cartService.GetCartItemCountAsync(userId);

            var response = new CartCountResponse
            {
                Count = count
            };

            return Ok(response);
        }

        /// <summary>
        /// Clear cart and add items from an order (for rent again functionality)
        /// </summary>
        [HttpPost("rent-again/{orderId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse<object>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
        public async Task<IActionResult> RentAgainToCart(Guid orderId)
        {
            try
            {
                var customerId = GetCustomerId();
                var (success, addedCount, issuesCount) = await _cartService.AddOrderItemsToCartAsync(customerId, orderId);

                if (!success)
                {
                    return BadRequest(new ApiResponse<object>("Failed to add order items to cart.", null));
                }

                string message;
                if (issuesCount > 0)
                {
                    message = $"{addedCount} item(s) added to cart. However, some items were skipped or have reduced quantity due to limited stock. Please review your cart before checkout.";
                }
                else
                {
                    message = $"All {addedCount} item(s) successfully added to cart. You can now update rental dates and proceed to checkout.";
                }

                return Ok(new ApiResponse<object>(message, new { addedCount, issuesCount }));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>(ex.Message, null));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<object>(ex.Message, null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>("Unable to add items to cart. Please try again later or contact support.", null));
            }
        }
    }
}