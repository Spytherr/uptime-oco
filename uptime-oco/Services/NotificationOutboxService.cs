using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace uptime_oco;

public class NotificationOutboxService(
    IServiceProvider serviceProvider,
    IOptions<MonitoringOptions> options,
    ILogger<NotificationOutboxService> logger) : BackgroundService
{
    private const int MaxBackoffSeconds = 300;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationOutboxService started");

        var pollInterval = TimeSpan.FromSeconds(
            Math.Max(options.Value.NotificationPollIntervalSeconds, 1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in NotificationOutboxService loop");

                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("NotificationOutboxService stopped");
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UptimeOcoContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var now = DateTime.UtcNow;

        var outbox = await context.NotificationOutboxes
            .Include(item => item.Incident)
            .ThenInclude(incident => incident!.Monitor)
            .Include(item => item.NotificationChannel)
            .Where(item => item.ProcessedAt == null
                && item.FailedAt == null
                && item.NextAttemptAt <= now)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (outbox is null)
        {
            return false;
        }

        try
        {
            var incident = outbox.Incident;
            var monitor = incident?.Monitor;
            var channel = outbox.NotificationChannel;

            if (incident is null || monitor is null || channel is null)
            {
                outbox.FailedAt = now;
                outbox.LastError = "Notification dependencies are no longer available.";
                logger.LogError("Notification outbox item {OutboxId} has missing dependencies", outbox.Id);
            }
            else if (!channel.IsEnabled)
            {
                outbox.ProcessedAt = now;
                logger.LogInformation(
                    "Skipping notification outbox item {OutboxId} because channel {ChannelId} is disabled",
                    outbox.Id,
                    channel.Id);
            }
            else
            {
                await notificationService.SendIncidentAsync(
                    outbox,
                    monitor,
                    channel,
                    cancellationToken);
                outbox.ProcessedAt = DateTime.UtcNow;
                outbox.LastError = null;

                logger.LogInformation(
                    "Processed notification outbox item {OutboxId} for channel {ChannelId}",
                    outbox.Id,
                    channel.Id);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            outbox.ProcessedAt = null;
            outbox.AttemptCount++;
            outbox.LastError = Truncate(ex.Message, 2000);

            var maxAttempts = Math.Max(options.Value.NotificationMaxAttempts, 1);
            if (outbox.AttemptCount >= maxAttempts)
            {
                outbox.FailedAt = DateTime.UtcNow;
                logger.LogError(
                    ex,
                    "Notification outbox item {OutboxId} failed permanently after {Attempts} attempts",
                    outbox.Id,
                    outbox.AttemptCount);
            }
            else
            {
                outbox.NextAttemptAt = DateTime.UtcNow.Add(GetRetryDelay(outbox.AttemptCount));
                logger.LogWarning(
                    ex,
                    "Notification outbox item {OutboxId} failed on attempt {Attempt}; retry scheduled for {NextAttemptAt}",
                    outbox.Id,
                    outbox.AttemptCount,
                    outbox.NextAttemptAt);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Min(Math.Max(attemptCount - 1, 0), 6);
        var delaySeconds = Math.Min(MaxBackoffSeconds, 5 * (1 << exponent));
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
