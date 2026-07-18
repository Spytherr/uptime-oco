namespace uptime_oco;

public interface INotificationService
{
    Task NotifyIncidentAsync(Incident incident, Monitor monitor, CancellationToken cancellationToken = default);
}
