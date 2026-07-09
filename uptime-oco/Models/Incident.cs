namespace uptime_oco;

public class Incident
{
    public int Id { get; set; }

    public int MonitorId { get; set; }

    public Monitor? Monitor { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public required string Reason { get; set; }
}
