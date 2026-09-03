using System.Net.Http.Json;

namespace uptime_oco;

public abstract class HttpNotificationSender(
    IHttpClientFactory httpClientFactory) : INotificationSender
{
    public abstract NotificationType Type { get; }

    protected abstract object CreatePayload(NotificationMessage message);

    public async Task SendAsync(
        string target,
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("notification");
        client.Timeout = TimeSpan.FromSeconds(5);

        using var response = await client.PostAsJsonAsync(
            target,
            CreatePayload(message),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
