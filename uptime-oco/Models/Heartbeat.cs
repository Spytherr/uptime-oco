namespace uptime_oco;

public class Heartbeat
{
    public int Id { get; set; }

    public int MonitorId { get; set; }

    public Monitor? Monitor { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public string? SourceIp { get; set; }
}
