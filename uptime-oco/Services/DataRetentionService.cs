using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace uptime_oco;

public class DataRetentionService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DataRetentionService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredPingResultsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DataRetentionService loop");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("DataRetentionService stopped");
    }

    private async Task DeleteExpiredPingResultsAsync(CancellationToken cancellationToken)
    {
        var configuredRetentionDays = configuration.GetValue<int?>("Monitoring:RetentionDays");
        var retentionDays = configuredRetentionDays is > 0 ? configuredRetentionDays.Value : 90;

        if (configuredRetentionDays is <= 0)
        {
            logger.LogWarning("Monitoring:RetentionDays must be greater than zero. Using the default of 90 days.");
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UptimeOcoContext>();
        var deletedCount = await context.PingResults
            .Where(p => p.CheckedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {Count} ping results older than {Cutoff}", deletedCount, cutoff);
        }
    }
}
