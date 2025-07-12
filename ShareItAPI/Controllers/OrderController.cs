using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.OrderServices;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize(Roles = "customer,provider")]
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

        [HttpPut("{orderId:guid}/mark-approved")]
        public async Task<IActionResult> MarkAsApproved(Guid orderId)
        {
            await _orderService.MarkAsApprovedAsync(orderId);
            return Ok(new ApiResponse<string>("Order marked as approved", null));
        }
        [HttpPut("{orderId:guid}/mark-shipping")]
        public async Task<IActionResult> MarkAsShipping(Guid orderId)
        {
            await _orderService.MarkAsShipingAsync(orderId);
            return Ok(new ApiResponse<string>("Order marked as shipping", null));
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
        public async Task<IActionResult> GetProviderDashboardStats(Guid providerId)
        {
            var stats = await _orderService.GetProviderDashboardStatsAsync(providerId);
            return Ok(new ApiResponse<object>("Provider dashboard statistics", stats));
        }

        [HttpGet("provider/{providerId:guid}")]
        public async Task<IActionResult> GetOrdersByProvider(Guid providerId)
        {
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
        public async Task<IActionResult> GetProviderOrdersForListDisplay(Guid providerId)
        {
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
        public async Task<IActionResult> GetOrderDetails(Guid orderId)
        {
            var orderDetails = await _orderService.GetOrderDetailsAsync(orderId);

            if (orderDetails == null)
            {
                return NotFound(new ApiResponse<object>($"Order with ID {orderId} not found.", null));
            }

            return Ok(new ApiResponse<OrderDetailsDto>("Order details retrieved successfully.", orderDetails));
        }
    }
}