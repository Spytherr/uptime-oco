namespace uptime_oco;

public class NotificationService(IEnumerable<INotificationSender> senders) : INotificationService
{
    private readonly IReadOnlyDictionary<NotificationType, INotificationSender> sendersByType =
        senders.ToDictionary(sender => sender.Type);

    public Task SendIncidentAsync(
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

        var message = new NotificationMessage(
            $"{title}: **{monitor.Name}**{duration}\nURL: {monitor.Url}\nReason: {notification.Reason}\nStatus: {status}",
            "uptime-oco");

        return GetSender(channel.Type).SendAsync(channel.Target, message, cancellationToken);
    }

    public Task SendTestAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessage(
            $"**Test notification** from uptime-oco\nChannel: {channel.Name}\nTime: {DateTime.UtcNow:O}",
            "uptime-oco test");

        return GetSender(channel.Type).SendAsync(channel.Target, message, cancellationToken);
    }

    private INotificationSender GetSender(NotificationType type)
    {
        if (!sendersByType.TryGetValue(type, out var sender))
        {
            throw new NotSupportedException($"Notification type '{type}' is not supported.");
        }

        return sender;
    }
}
