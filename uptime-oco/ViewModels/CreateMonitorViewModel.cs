namespace uptime_oco;

public class CreateMonitorViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 60;
    public int RetryThreshold { get; set; } = 3;
}
