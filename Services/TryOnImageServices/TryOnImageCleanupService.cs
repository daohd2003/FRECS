using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.TryOnImageServices
{
    /// <summary>
    /// Background service tự động xóa ảnh Try-On đã hết hạn
    /// Chạy mỗi ngày 1 lần vào lúc 2:00 AM UTC để tiết kiệm tài nguyên server
    /// </summary>
    public class TryOnImageCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TryOnImageCleanupService> _logger;

        // Giờ chạy cleanup (2:00 AM UTC)
        private const int CleanupHour = 2;
        private const int CleanupMinute = 0;

        public TryOnImageCleanupService(
            IServiceProvider serviceProvider,
            ILogger<TryOnImageCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Try-On Image Cleanup Service started. Scheduled to run daily at {Hour}:{Minute} UTC", CleanupHour, CleanupMinute);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CalculateDelayUntilNextRun();
                _logger.LogDebug("Next cleanup scheduled in {Hours} hours", delay.TotalHours.ToString("F1"));

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await DoCleanupAsync();
                }
                catch (TaskCanceledException)
                {
                    // Service is stopping, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Try-On image cleanup");
                    // Wait 1 hour before retrying if error occurs
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Try-On Image Cleanup Service stopped");
        }

        private TimeSpan CalculateDelayUntilNextRun()
        {
            var now = DateTime.UtcNow;
            var nextRun = new DateTime(now.Year, now.Month, now.Day, CleanupHour, CleanupMinute, 0, DateTimeKind.Utc);

            // Nếu đã qua giờ chạy hôm nay, chạy vào ngày mai
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            return nextRun - now;
        }

        private async Task DoCleanupAsync()
        {
            _logger.LogInformation("Starting daily Try-On image cleanup at {Time} UTC", DateTime.UtcNow);

            using var scope = _serviceProvider.CreateScope();
            var tryOnImageService = scope.ServiceProvider.GetRequiredService<ITryOnImageService>();

            var deletedCount = await tryOnImageService.CleanupExpiredImagesAsync();

            _logger.LogInformation("Daily cleanup completed: {Count} expired Try-On images deleted", deletedCount);
        }
    }
}
