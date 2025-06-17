using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.OrdersDto;
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
    [Authorize(Roles = "customer")]
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CartDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var customerId = GetCustomerId();
                var cart = await _cartService.GetUserCartAsync(customerId);

                if (cart == null)
                {
                    return Ok(new CartDto { CustomerId = customerId, Items = new System.Collections.Generic.List<CartItemDto>(), TotalAmount = 0 });
                }

                return Ok(cart);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Add product to cart
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddToCart([FromBody] CartAddRequestDto dto)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.AddProductToCartAsync(customerId, dto);

                if (!result)
                {
                    return BadRequest("Failed to add product to cart.");
                }

                var updatedCart = await _cartService.GetUserCartAsync(customerId);
                return CreatedAtAction(nameof(GetCart), updatedCart);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update cart item quantity and duration
        /// </summary>
        [HttpPut("{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] CartUpdateRequestDto dto)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.UpdateCartItemAsync(customerId, itemId, dto);

                if (!result)
                {
                    return NotFound("Cart item not found or does not belong to the current user.");
                }

                return Ok(new { message = "Cart item updated successfully." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("{itemId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveFromCart(Guid itemId)
        {
            try
            {
                var customerId = GetCustomerId();
                var result = await _cartService.RemoveCartItemAsync(customerId, itemId);

                if (!result)
                {
                    return NotFound("Cart item not found or does not belong to the current user.");
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Initiates the checkout process from the current user's cart, creating an order.
        /// </summary>
        [HttpPost("checkout")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(OrderDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto checkoutRequestDto)
        {
            try
            {
                var customerId = GetCustomerId();
                var createdOrderDto = await _orderService.CreateOrderFromCartAsync(customerId, checkoutRequestDto);

                return CreatedAtAction(
                    "GetOrderDetail",
                    "Order",
                    new { orderId = createdOrderDto.Id },
                    createdOrderDto
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred." });
            }
        }
    }
}
