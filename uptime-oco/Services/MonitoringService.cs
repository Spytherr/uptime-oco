using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace uptime_oco;

public class MonitoringService(
    UptimeOcoContext context,
    IHttpClientFactory httpClientFactory,
    IHubContext<MonitorHub> hubContext,
    IOptions<MonitoringOptions> monitoringOptions,
    ILogger<MonitoringService> logger) : IMonitoringService
{
    private HttpClient CreateClient(int timeoutSeconds)
    {
        var client = httpClientFactory.CreateClient("monitor");
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("uptime-oco/1.0");
        return client;
    }

    public async Task<PingResult> CheckMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
    {
        var ping = await PerformHttpCheckAsync(monitor, cancellationToken);
        await ProcessPingResultAsync(monitor, ping, cancellationToken);
        return ping;
    }

    private async Task<PingResult> PerformHttpCheckAsync(Monitor monitor, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        PingResult ping;

        try
        {
            var client = CreateClient(monitor.TimeoutSeconds);
            using var response = await client.GetAsync(
                monitor.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            sw.Stop();

            var statusCode = (int)response.StatusCode;
            var isSuccess = statusCode == monitor.ExpectedStatusCode;

            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                HttpStatusCode = statusCode,
                IsSuccess = isSuccess,
                FailureReason = isSuccess
                    ? null
                    : $"Expected HTTP {monitor.ExpectedStatusCode}, received HTTP {statusCode}",
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                HttpStatusCode = null,
                IsSuccess = false,
                FailureReason = $"Request timed out after {monitor.TimeoutSeconds} seconds",
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "HTTP check failed for monitor {MonitorId}", monitor.Id);

            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = null,
                HttpStatusCode = null,
                IsSuccess = false,
                FailureReason = "Connection failed",
                CheckedAt = DateTime.UtcNow
            };
        }

        return ping;
    }

    private async Task ProcessPingResultAsync(Monitor monitor, PingResult ping, CancellationToken cancellationToken)
    {
        context.PingResults.Add(ping);

        monitor.LastCheckAt = ping.CheckedAt;

        if (ping.IsSuccess)
        {
            if (monitor.ConsecutiveFailures > 0)
            {
                logger.LogInformation("Monitor {MonitorId} recovered after {Failures} failures", monitor.Id, monitor.ConsecutiveFailures);
            }
            monitor.ConsecutiveFailures = 0;
        }
        else
        {
            monitor.ConsecutiveFailures++;
        }

        var incident = await HandleIncidentAsync(monitor, ping, cancellationToken);
        var status = MonitorStatusCalculator.Calculate(
            monitor,
            hasCheckResult: true,
            hasOpenIncident: incident is { ResolvedAt: null });

        if (incident is not null)
        {
            var channels = await context.NotificationChannels
                .Where(c => c.UserId == monitor.UserId && c.IsEnabled)
                .ToListAsync(cancellationToken);

            foreach (var channel in channels)
            {
                context.NotificationOutboxes.Add(new NotificationOutbox
                {
                    Incident = incident,
                    NotificationChannelId = channel.Id,
                    Reason = incident.Reason,
                    StartedAt = incident.StartedAt,
                    ResolvedAt = incident.ResolvedAt,
                    IsResolved = incident.ResolvedAt.HasValue
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.User(monitor.UserId).SendAsync("PingReceived", new
        {
            monitorId = monitor.Id,
            monitorName = monitor.Name,
            status = status.ToString(),
            isSuccess = ping.IsSuccess,
            responseTimeMs = ping.ResponseTimeMs,
            httpStatusCode = ping.HttpStatusCode,
            checkedAt = ping.CheckedAt,
            consecutiveFailures = monitor.ConsecutiveFailures
        }, cancellationToken);

        if (incident is not null)
        {
            await hubContext.Clients.User(monitor.UserId).SendAsync("IncidentUpdate", new
            {
                monitorId = monitor.Id,
                monitorName = monitor.Name,
                incidentId = incident.Id,
                reason = incident.Reason,
                isResolved = incident.ResolvedAt.HasValue,
                startedAt = incident.StartedAt,
                resolvedAt = incident.ResolvedAt
            }, cancellationToken);
        }
    }

    public async Task CheckAllMonitorsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var monitors = await context.Monitors
            .Where(m => m.IsActive && (m.LastCheckAt == null || m.LastCheckAt.Value.AddSeconds(m.IntervalSeconds) <= now))
            .ToListAsync(cancellationToken);

        if (monitors.Count == 0)
        {
            return;
        }

        logger.LogInformation("Checking {Count} due monitors", monitors.Count);

        var maxConcurrentChecks = Math.Max(monitoringOptions.Value.MaxConcurrentChecks, 1);
        using var semaphore = new SemaphoreSlim(maxConcurrentChecks);
        var checkTasks = monitors.Select(async monitor =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return (monitor, ping: await PerformHttpCheckAsync(monitor, cancellationToken));
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();
        var results = await Task.WhenAll(checkTasks);

        foreach (var (monitor, ping) in results)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessPingResultAsync(monitor, ping, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing result for monitor {MonitorId}", monitor.Id);
            }
        }
    }

    private async Task<Incident?> HandleIncidentAsync(Monitor monitor, PingResult ping, CancellationToken cancellationToken)
    {
        if (!ping.IsSuccess && monitor.ConsecutiveFailures >= monitor.RetryThreshold)
        {
            var openIncident = await context.Incidents
                .FirstOrDefaultAsync(i => i.MonitorId == monitor.Id && i.ResolvedAt == null, cancellationToken);

            if (openIncident is null)
            {
                var reason = ping.FailureReason ?? (ping.HttpStatusCode.HasValue
                    ? $"HTTP {ping.HttpStatusCode.Value}"
                    : "Connection failed");

                openIncident = new Incident
                {
                    MonitorId = monitor.Id,
                    StartedAt = ping.CheckedAt,
                    Reason = reason
                };

                context.Incidents.Add(openIncident);
                logger.LogWarning("Incident opened for monitor {MonitorId}: {Reason}", monitor.Id, reason);
                return openIncident;
            }
        }

        if (ping.IsSuccess)
        {
            var openIncident = await context.Incidents
                .FirstOrDefaultAsync(i => i.MonitorId == monitor.Id && i.ResolvedAt == null, cancellationToken);

            if (openIncident is not null)
            {
                openIncident.ResolvedAt = ping.CheckedAt;
                logger.LogInformation("Incident resolved for monitor {MonitorId}", monitor.Id);
                return openIncident;
            }
        }

        return null;
    }
}
