namespace uptime_oco;

public class MonitoringOptions
{
    public int MaxConcurrentChecks { get; set; } = 10;

    public int NotificationPollIntervalSeconds { get; set; } = 5;

    public int NotificationMaxAttempts { get; set; } = 5;
}
