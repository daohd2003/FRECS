using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.NotificationServices;

namespace Services.OrderServices
{
    /// <summary>
    /// Background service để tự động chuyển order status từ in_transit sang in_use
    /// Chạy mỗi ngày 1 lần
    /// 
    /// Logic:
    /// - Rental orders: Chuyển khi đến RentalStartDate HOẶC sau 48 giờ kể từ DeliveredDate (lấy điều kiện nào đến trước)
    /// - Purchase orders: Chuyển sau 7 ngày kể từ DeliveredDate
    /// </summary>
    public class OrderStatusAutoUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderStatusAutoUpdateService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Check every day

        // Configuration constants
        private const int RENTAL_AUTO_CONFIRM_HOURS = 48; // 48 hours for rental orders
        private const int PURCHASE_AUTO_CONFIRM_DAYS = 7; // 7 days for purchase orders

        public OrderStatusAutoUpdateService(
            IServiceProvider serviceProvider,
            ILogger<OrderStatusAutoUpdateService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Order Status Auto Update Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await AutoUpdateOrderStatuses();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Order Status Auto Update Service is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while auto-updating order statuses.");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait 30 minutes before retrying
                }
            }
        }


        private async Task AutoUpdateOrderStatuses()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShareItDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            var now = DateTimeHelper.GetVietnamTime();

            try
            {
                // Get all orders with status in_transit
                var inTransitOrders = await context.Orders
                    .Include(o => o.Items)
                    .Where(o => o.Status == OrderStatus.in_transit)
                    .ToListAsync();

                var ordersToNotify = new List<(Order order, string reason)>();

                foreach (var order in inTransitOrders)
                {
                    bool shouldAutoConfirm = false;
                    string reason = "";

                    // Check if order has rental items
                    var hasRentalItems = order.Items.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental);
                    var isPurchaseOnly = order.Items.All(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase);

                    if (hasRentalItems)
                    {
                        // Rental order logic:
                        // 1. Auto-confirm when RentalStartDate arrives
                        // 2. OR auto-confirm after 48 hours from DeliveredDate
                        // Whichever comes first

                        if (order.RentalStart.HasValue && now >= order.RentalStart.Value)
                        {
                            shouldAutoConfirm = true;
                            reason = "rental start date has arrived";
                        }
                        else if (order.DeliveredDate.HasValue)
                        {
                            var hoursElapsed = (now - order.DeliveredDate.Value).TotalHours;
                            if (hoursElapsed >= RENTAL_AUTO_CONFIRM_HOURS)
                            {
                                shouldAutoConfirm = true;
                                reason = $"{RENTAL_AUTO_CONFIRM_HOURS} hours have passed since delivery";
                            }
                        }
                    }
                    else if (isPurchaseOnly)
                    {
                        // Purchase order logic:
                        // Auto-confirm after 7 days from DeliveredDate
                        if (order.DeliveredDate.HasValue)
                        {
                            var daysElapsed = (now - order.DeliveredDate.Value).TotalDays;
                            if (daysElapsed >= PURCHASE_AUTO_CONFIRM_DAYS)
                            {
                                shouldAutoConfirm = true;
                                reason = $"{PURCHASE_AUTO_CONFIRM_DAYS} days have passed since delivery";
                            }
                        }
                    }

                    if (shouldAutoConfirm)
                    {
                        order.Status = OrderStatus.in_use;
                        order.UpdatedAt = now;
                        ordersToNotify.Add((order, reason));

                        _logger.LogInformation(
                            "Auto-confirmed order {OrderId} from in_transit to in_use. Reason: {Reason}",
                            order.Id, reason);
                    }
                }

                if (ordersToNotify.Count > 0)
                {
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Auto-updated {Count} orders from in_transit to in_use.", ordersToNotify.Count);

                    // Send notifications for each auto-confirmed order
                    foreach (var (order, reason) in ordersToNotify)
                    {
                        await SendAutoConfirmNotifications(order, reason, notificationService, hubContext);
                    }
                }
                else
                {
                    _logger.LogDebug("No orders needed auto-update.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-update order statuses.");
                throw;
            }
        }

        private async Task SendAutoConfirmNotifications(
            Order order,
            string reason,
            INotificationService notificationService,
            IHubContext<NotificationHub> hubContext)
        {
            var orderCode = $"ORD-{order.Id.ToString().Substring(0, 8).ToUpper()}";
            
            // Message for customer
            var customerMessage = $"Order #{orderCode} has been automatically confirmed as received because {reason}. " +
                                  "If you haven't received your order, please contact support immediately.";
            
            // Message for provider
            var providerMessage = $"Order #{orderCode} has been automatically confirmed as received by the system ({reason}).";

            try
            {
                // Send notification to customer
                await notificationService.SendNotification(
                    order.CustomerId,
                    customerMessage,
                    NotificationType.order,
                    order.Id);

                // Send notification to provider
                await notificationService.SendNotification(
                    order.ProviderId,
                    providerMessage,
                    NotificationType.order,
                    order.Id);

                // Send real-time notifications via SignalR
                await hubContext.Clients.Group($"notifications-{order.CustomerId}")
                    .SendAsync("ReceiveNotification", customerMessage);
                
                await hubContext.Clients.Group($"notifications-{order.ProviderId}")
                    .SendAsync("ReceiveNotification", providerMessage);

                _logger.LogDebug("Sent auto-confirm notifications for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send auto-confirm notifications for order {OrderId}", order.Id);
                // Don't throw - notifications are not critical
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Order Status Auto Update Service is stopping.");
            await base.StopAsync(stoppingToken);
        }
    }
}
