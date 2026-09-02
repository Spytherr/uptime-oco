using System.Net.Http.Json;

namespace uptime_oco;

public class NotificationService(IHttpClientFactory httpClientFactory) : INotificationService
{
    public async Task SendIncidentAsync(
        NotificationOutbox notification,
        Monitor monitor,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var title = notification.IsResolved ? "Monitor Recovered" : "Monitor Down";
        var status = notification.IsResolved ? "UP" : "DOWN";
        var duration = notification.IsResolved && notification.ResolvedAt.HasValue
            ? $" (down for {(notification.ResolvedAt.Value - notification.StartedAt).TotalMinutes:F0} min)"
            : "";

        var message = $"{title}: **{monitor.Name}**{duration}\nURL: {monitor.Url}\nReason: {notification.Reason}\nStatus: {status}";

        await SendAsync(channel, message, cancellationToken);
    }

    private async Task SendAsync(NotificationChannel channel, string message, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("notification");
        client.Timeout = TimeSpan.FromSeconds(5);

        using var response = channel.Type == NotificationType.Discord
            ? await client.PostAsJsonAsync(
                channel.Target,
                new { content = message },
                cancellationToken)
            : await client.PostAsJsonAsync(
                channel.Target,
                new { text = message, title = "uptime-oco" },
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
