using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace uptime_oco;

public class NotificationService(
    UptimeOcoContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyIncidentAsync(Incident incident, Monitor monitor, CancellationToken cancellationToken = default)
    {
        var channels = await context.NotificationChannels
            .Where(c => c.UserId == monitor.UserId && c.IsEnabled)
            .ToListAsync(cancellationToken);

        if (channels.Count == 0)
        {
            return;
        }

        var isResolved = incident.ResolvedAt.HasValue;
        var title = isResolved ? "✅ Monitor Recovered" : "🔴 Monitor Down";
        var status = isResolved ? "UP" : "DOWN";
        var duration = isResolved && incident.ResolvedAt.HasValue
            ? $" (down for {(incident.ResolvedAt.Value - incident.StartedAt).TotalMinutes:F0} min)"
            : "";

        var message = $"{title}: **{monitor.Name}**{duration}\nURL: {monitor.Url}\nReason: {incident.Reason}\nStatus: {status}";

        foreach (var channel in channels)
        {
            try
            {
                await SendAsync(channel, message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send notification to channel {ChannelId} ({Type})", channel.Id, channel.Type);
            }
        }
    }

    private async Task SendAsync(NotificationChannel channel, string message, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("notification");
        client.Timeout = TimeSpan.FromSeconds(5);

        if (channel.Type == NotificationType.Discord)
        {
            var payload = new { content = message };
            await client.PostAsJsonAsync(channel.Target, payload, cancellationToken);
        }
        else
        {
            var payload = new { text = message, title = "uptime-oco" };
            await client.PostAsJsonAsync(channel.Target, payload, cancellationToken);
        }
    }
}
