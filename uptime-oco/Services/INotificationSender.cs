namespace uptime_oco;

public interface INotificationSender
{
    NotificationType Type { get; }

    Task SendAsync(
        string target,
        NotificationMessage message,
        CancellationToken cancellationToken = default);
}
