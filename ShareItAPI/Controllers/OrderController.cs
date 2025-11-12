using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.OrderServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize(Roles = "customer,provider,admin,staff")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            await _orderService.CreateOrderAsync(dto);
            return Ok(new ApiResponse<string>("Order created", null));
        }

        [HttpPut("{orderId:guid}/status")]
        public async Task<IActionResult> ChangeOrderStatus(Guid orderId, [FromQuery] OrderStatus newStatus)
        {
            await _orderService.ChangeOrderStatus(orderId, newStatus);
            return Ok(new ApiResponse<string>($"Order status changed to {newStatus}", null));
        }

        [HttpPut("{orderId:guid}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid orderId)
        {
            await _orderService.CancelOrderAsync(orderId);
            return Ok(new ApiResponse<string>("Order cancelled", null));
        }

        [HttpDelete("{orderId:guid}")]
        public async Task<IActionResult> DeleteOrder(Guid orderId)
        {
            try
            {
                await _orderService.DeleteOrderAsync(orderId);
                return Ok(new ApiResponse<string>("Order deleted permanently", null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpPut("{orderId:guid}/items")]
        public async Task<IActionResult> UpdateOrderItems(Guid orderId, [FromBody] List<Guid> updatedItemIds, int rentalDays)
        {
            await _orderService.UpdateOrderItemsAsync(orderId, updatedItemIds, rentalDays);
            return Ok(new ApiResponse<string>("Order items updated", null));
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetAllOrdersDetails()
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(new ApiResponse<object>("All orders detail retrieved", orders));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _orderService.GetAllAsync();
            return Ok(new ApiResponse<object>("All orders retrieved", orders));
        }

        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetOrdersByStatus(OrderStatus status)
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);
            return Ok(new ApiResponse<object>($"Orders with status {status}", orders));
        }

        [HttpGet("{orderId:guid}")]
        public async Task<IActionResult> GetOrderDetail(Guid orderId)
        {
            var order = await _orderService.GetOrderDetailAsync(orderId);
            return Ok(new ApiResponse<object>("Order detail retrieved", order));
        }

        [HttpPut("{orderId:guid}/mark-received")]
        public async Task<IActionResult> MarkAsReceived(Guid orderId, [FromQuery] bool paid)
        {
            await _orderService.MarkAsReceivedAsync(orderId, paid);
            return Ok(new ApiResponse<string>($"Order marked as received (Paid: {paid})", null));
        }

        [HttpPut("{orderId:guid}/mark-returned")]
        public async Task<IActionResult> MarkAsReturned(Guid orderId)
        {
            await _orderService.MarkAsReturnedAsync(orderId);
            return Ok(new ApiResponse<string>("Order marked as returned", null));
        }

        [HttpPut("{orderId:guid}/mark-returning")]
        public async Task<IActionResult> MarkAsReturning(Guid orderId)
        {
            try
            {
                await _orderService.MarkAsReturningAsync(orderId);
                return Ok(new ApiResponse<string>("Order marked as returning", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        // NOTE: This endpoint is for ADMIN MANUAL INTERVENTION only
        // Payment system (VNPay/SEPay) calls ChangeOrderStatus service method directly in IpnAction callback
        // This endpoint is kept for admin to manually approve orders in case of payment issues
        // Providers should NOT be able to manually call this endpoint
        [HttpPut("{orderId:guid}/mark-approved")]
        [Authorize(Roles = "admin")] // Only admin can manually approve orders
        public async Task<IActionResult> MarkAsApproved(Guid orderId)
        {
            try
            {
                await _orderService.MarkAsApprovedAsync(orderId);
                return Ok(new ApiResponse<string>("Order marked as approved", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }
        [HttpPut("{orderId:guid}/mark-shipping")]
        public async Task<IActionResult> MarkAsShipping(Guid orderId)
        {
            await _orderService.MarkAsShipingAsync(orderId);
            return Ok(new ApiResponse<string>("Order marked as shipping", null));
        }

        [HttpPut("{orderId:guid}/confirm-delivery")]
        public async Task<IActionResult> ConfirmDelivery(Guid orderId)
        {
            try
            {
                await _orderService.ConfirmDeliveryAsync(orderId);
                return Ok(new ApiResponse<string>("Order delivery confirmed successfully", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _orderService.GetDashboardStatsAsync();
            return Ok(new ApiResponse<object>("Dashboard statistics", stats));
        }

        // Endpoint cho CUSTOMER
        [HttpGet("customer/dashboard-stats/{customerId}")]
        public async Task<IActionResult> GetCustomerDashboardStats(Guid customerId)
        {
            var stats = await _orderService.GetCustomerDashboardStatsAsync(customerId);
            return Ok(new ApiResponse<object>("Customer dashboard statistics", stats));
        }

        // Endpoint cho PROVIDER
        [HttpGet("provider/dashboard-stats/{providerId}")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetProviderDashboardStats(Guid providerId)
        {
            // Verify the authenticated user is the provider or an admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated.", null));
            }

            // Allow admins to view any provider's stats, but providers can only view their own
            if (!User.IsInRole("admin") && currentUserId != providerId.ToString())
            {
                return Forbid();
            }

            var stats = await _orderService.GetProviderDashboardStatsAsync(providerId);
            return Ok(new ApiResponse<object>("Provider dashboard statistics", stats));
        }

        [HttpGet("provider/{providerId:guid}")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetOrdersByProvider(Guid providerId)
        {
            // Verify the authenticated user is the provider or an admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated.", null));
            }

            // Allow admins to view any provider's orders, but providers can only view their own
            if (!User.IsInRole("admin") && currentUserId != providerId.ToString())
            {
                return Forbid();
            }

            var orders = await _orderService.GetOrdersByProviderAsync(providerId);
            return Ok(new ApiResponse<object>("Orders by provider", orders));
        }

        [HttpPut("{orderId:guid}/mark-returnedwithissue")]
        public async Task<IActionResult> MarkAsReturnedWithIssue(Guid orderId)
        {
            await _orderService.MarkAsReturnedWithIssueAsync(orderId);
            return Ok(new ApiResponse<string>("Order marked as returned_with_issue. ", null));
        }

        // NEW: Endpoint to get orders for list display (by provider)
        [HttpGet("provider/{providerId:guid}/list-display")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetProviderOrdersForListDisplay(Guid providerId)
        {
            // Verify the authenticated user is the provider or an admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated.", null));
            }

            // Allow admins to view any provider's orders, but providers can only view their own
            if (!User.IsInRole("admin") && currentUserId != providerId.ToString())
            {
                return Forbid();
            }

            var orders = await _orderService.GetProviderOrdersForListDisplayAsync(providerId);
            return Ok(new ApiResponse<object>($"Orders for provider {providerId} list display retrieved", orders));
        }

        // NEW: Endpoint to get orders for list display (by customer)
        [HttpGet("customer/{customerId:guid}/list-display")]
        public async Task<IActionResult> GetCustomerOrdersForListDisplay(Guid customerId)
        {
            var orders = await _orderService.GetCustomerOrdersForListDisplayAsync(customerId);
            return Ok(new ApiResponse<object>($"Orders for customer {customerId} list display retrieved", orders));
        }

        // NEW: Endpoint to get orders for list display (by customer)
        [HttpGet("customer/{customerId:guid}/list-orders")]
        public async Task<IActionResult> GetCustomerOrders(Guid customerId)
        {
            var orders = await _orderService.GetCustomerOrdersAsync(customerId);
            return Ok(new ApiResponse<object>($"Orders for customer {customerId} list display retrieved", orders));
        }

        // GET: api/orders/{orderId}/details
        [HttpGet("{orderId:guid}/details")]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> GetOrderDetails(Guid orderId)
        {
            var orderDetails = await _orderService.GetOrderDetailsAsync(orderId);

            if (orderDetails == null)
            {
                return NotFound(new ApiResponse<object>($"Order with ID {orderId} not found.", null));
            }

            // Verify the authenticated user (customer or provider acting as customer) owns this order
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated.", null));
            }

            // Get the order entity to check CustomerId
            var orderEntity = await _orderService.GetOrderEntityByIdAsync(orderId);
            if (orderEntity == null)
            {
                return NotFound(new ApiResponse<object>($"Order with ID {orderId} not found.", null));
            }

            // Verify the order belongs to the current user (as customer)
            if (orderEntity.CustomerId.ToString() != currentUserId)
            {
                return Forbid();
            }

            return Ok(new ApiResponse<OrderDetailsDto>("Order details retrieved successfully.", orderDetails));
        }

        [HttpGet("{orderId:guid}/provider-details")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetOrderDetailsForProvider(Guid orderId)
        {
            var orderDetails = await _orderService.GetOrderDetailsForProviderAsync(orderId);

            if (orderDetails == null)
            {
                return NotFound(new ApiResponse<object>($"Order with ID {orderId} not found.", null));
            }

            // Verify the authenticated user is the provider who owns this order or an admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated.", null));
            }

            // Allow admins to view any order, but providers can only view their own orders
            if (!User.IsInRole("admin") && currentUserId != orderDetails.ProviderId.ToString())
            {
                return Forbid();
            }

            return Ok(new ApiResponse<OrderDetailsDto>("Provider order details retrieved successfully.", orderDetails));
        }

        [HttpPost("rent-again")]
        public async Task<IActionResult> RentAgain([FromBody] RentAgainRequestDto requestDto)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (customerIdClaim == null || !Guid.TryParse(customerIdClaim.Value, out var customerId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user ID. Please log in again.", null));
            }

            try
            {
                Guid newOrderId = await _orderService.RentAgainOrderAsync(customerId, requestDto);

                return Ok(new ApiResponse<Guid>("Order successfully created for rent again!", newOrderId));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An unexpected server error occurred: {ex.Message}", null));
            }
        }

        [HttpPut("update-contact-info")]
        public async Task<IActionResult> UpdateOrderContactInfo([FromBody] UpdateOrderContactInfoDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Message = "Invalid data provided.",
                    Data = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Message = "User not authenticated.",
                    Data = null
                });
            }

            var customerId = Guid.Parse(userIdClaim);

            try
            {
                var isSuccess = await _orderService.UpdateOrderContactInfoAsync(customerId, requestDto);

                if (!isSuccess)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Message = "Failed to update contact information. The order may not exist, may not belong to you, or may not be in the 'pending' status.",
                        Data = null
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Message = "Contact information updated successfully.",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Message = "An internal server error occurred.",
                    Data = null
                });
            }
        }

        [HttpGet("order-item/{productId}")]
        public async Task<IActionResult> GetOrderItemId([FromRoute] Guid productId)
        {
            var customerId = GetCurrentUserId();
            var guidString = await _orderService.GetOrderItemId(customerId,productId);
            
            return Ok(new ApiResponse<string>("Get Order Item Successfully ", guidString));
        }

        // Temporary endpoint to fix existing orders with missing subtotals
        [HttpPost("fix-subtotals")]
        [Authorize(Roles = "admin")] // Only admin can call this
        public async Task<IActionResult> FixOrderSubtotals()
        {
            try
            {
                await _orderService.UpdateOrderSubtotalsAsync();
                return Ok(new ApiResponse<string>("Order subtotals updated successfully", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error updating subtotals: {ex.Message}", null));
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                throw new InvalidOperationException("User ID from authentication token is missing or invalid.");
            }
            return userId;
        }

        /// <summary>
        /// Get all orders for admin management
        /// </summary>
        [HttpGet("admin/all-orders")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAllOrdersForAdmin()
        {
            try
            {
                var orders = await _orderService.GetAllOrdersForAdminAsync();
                return Ok(new ApiResponse<IEnumerable<AdminOrderListDto>>("All orders retrieved successfully", orders));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error retrieving orders: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Get detailed order information for admin
        /// </summary>
        [HttpGet("admin/{orderId:guid}/detail")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetOrderDetailForAdmin(Guid orderId)
        {
            try
            {
                var orderDetail = await _orderService.GetOrderDetailForAdminAsync(orderId);
                
                if (orderDetail == null)
                {
                    return NotFound(new ApiResponse<string>($"Order with ID {orderId} not found", null));
                }
                
                return Ok(new ApiResponse<AdminOrderDetailDto>("Order detail retrieved successfully", orderDetail));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error retrieving order detail: {ex.Message}", null));
            }
        }
    }
}