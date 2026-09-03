namespace uptime_oco;

public interface INotificationService
{
    Task SendIncidentAsync(
        NotificationOutbox notification,
        Monitor monitor,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);

    Task SendTestAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken = default);
}
