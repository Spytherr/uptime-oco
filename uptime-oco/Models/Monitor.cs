namespace uptime_oco;

public class Monitor
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Url { get; set; }

    public MonitorType Type { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    public int GracePeriodSeconds { get; set; } = 300;

    public int RetryThreshold { get; set; } = 3;

    public int ConsecutiveFailures { get; set; }

    public string HeartbeatToken { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastCheckAt { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<PingResult> PingResults { get; set; } = [];

    public ICollection<Heartbeat> Heartbeats { get; set; } = [];

    public ICollection<Incident> Incidents { get; set; } = [];
}
