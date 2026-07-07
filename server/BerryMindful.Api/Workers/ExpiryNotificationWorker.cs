using BerryMindful.Services.NotificationServices;
using Microsoft.Extensions.Options;

namespace BerryMindful.Api.Workers;

// Hosted service that runs the expiry digest once a day at the configured local hour
// (RSS worker pattern). NotificationLogs dedupe makes reruns safe.
public class ExpiryNotificationWorker(
    IServiceProvider services,
    IOptions<NotificationOptions> options,
    ILogger<ExpiryNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.RunOnStartup)
        {
            await RunDigestAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTime.Now, options.Value.DailyHourLocal);
            logger.LogInformation("Next expiry digest run in {Delay:hh\\:mm\\:ss}.", delay);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            await RunDigestAsync(stoppingToken);
        }
    }

    internal static TimeSpan DelayUntilNextRun(DateTime localNow, int dailyHourLocal)
    {
        var nextRun = localNow.Date.AddHours(dailyHourLocal);
        if (nextRun <= localNow)
        {
            nextRun = nextRun.AddDays(1);
        }
        return nextRun - localNow;
    }

    private async Task RunDigestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var digestService = scope.ServiceProvider.GetRequiredService<ExpiryNotificationService>();
            var sent = await digestService.SendDigestsAsync(cancellationToken);
            logger.LogInformation("Expiry digest run complete — {Count} email(s) sent.", sent);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Expiry digest run failed; will retry on the next scheduled run.");
        }
    }
}
