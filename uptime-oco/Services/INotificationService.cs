namespace uptime_oco;

public interface INotificationService
{
    Task SendIncidentAsync(
        NotificationOutbox notification,
        Monitor monitor,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);
}
