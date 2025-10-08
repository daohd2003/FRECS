using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.DiscountCodeServices
{
    public class DiscountCodeExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DiscountCodeExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12); // Check every 12 hours

        public DiscountCodeExpirationService(
            IServiceProvider serviceProvider,
            ILogger<DiscountCodeExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Discount Code Expiration Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateExpiredDiscountCodes();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Discount Code Expiration Service is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while updating expired discount codes.");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying
                }
            }
        }

        private async Task UpdateExpiredDiscountCodes()
        {
            using var scope = _serviceProvider.CreateScope();
            var discountCodeService = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();

            try
            {
                await discountCodeService.UpdateExpiredStatusAsync();
                _logger.LogDebug("Successfully updated expired discount codes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update expired discount codes.");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Discount Code Expiration Service is stopping.");
            await base.StopAsync(stoppingToken);
        }
    }
}
