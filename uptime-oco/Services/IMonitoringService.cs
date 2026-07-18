namespace uptime_oco;

public interface IMonitoringService
{
    Task<PingResult> CheckMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default);
    Task CheckAllMonitorsAsync(CancellationToken cancellationToken = default);
}
