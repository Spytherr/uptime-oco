using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace uptime_oco;

public class MonitoringService(
    UptimeOcoContext context,
    IHttpClientFactory httpClientFactory,
    INotificationService notificationService,
    IHubContext<MonitorHub> hubContext,
    ILogger<MonitoringService> logger) : IMonitoringService
{
    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("monitor");
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("uptime-oco/1.0");
        return client;
    }

    public async Task<PingResult> CheckMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        PingResult ping;

        try
        {
            var client = CreateClient();
            var response = await client.GetAsync(monitor.Url, cancellationToken);
            sw.Stop();

            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                HttpStatusCode = (int)response.StatusCode,
                IsSuccess = response.IsSuccessStatusCode,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                HttpStatusCode = null,
                IsSuccess = false,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "HTTP check failed for monitor {MonitorId} ({Url})", monitor.Id, monitor.Url);

            ping = new PingResult
            {
                MonitorId = monitor.Id,
                ResponseTimeMs = null,
                HttpStatusCode = null,
                IsSuccess = false,
                CheckedAt = DateTime.UtcNow
            };
        }

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

        await context.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.All.SendAsync("PingReceived", new
        {
            monitorId = monitor.Id,
            monitorName = monitor.Name,
            isSuccess = ping.IsSuccess,
            responseTimeMs = ping.ResponseTimeMs,
            httpStatusCode = ping.HttpStatusCode,
            checkedAt = ping.CheckedAt,
            consecutiveFailures = monitor.ConsecutiveFailures
        }, cancellationToken);

        if (incident is not null)
        {
            await notificationService.NotifyIncidentAsync(incident, monitor, cancellationToken);

            await hubContext.Clients.All.SendAsync("IncidentUpdate", new
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

        return ping;
    }

    public async Task CheckAllMonitorsAsync(CancellationToken cancellationToken = default)
    {
        var monitors = await context.Monitors
            .Where(m => m.IsActive)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Checking {Count} active monitors", monitors.Count);

        foreach (var monitor in monitors)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await CheckMonitorAsync(monitor, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking monitor {MonitorId}", monitor.Id);
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
                var reason = ping.HttpStatusCode.HasValue
                    ? $"HTTP {ping.HttpStatusCode.Value}"
                    : "Connection failed";

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
