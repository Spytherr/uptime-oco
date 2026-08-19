namespace uptime_oco;

public class Monitor
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Url { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    public int ExpectedStatusCode { get; set; } = 200;

    public int TimeoutSeconds { get; set; } = 10;

    public int RetryThreshold { get; set; } = 3;

    public int ConsecutiveFailures { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastCheckAt { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<PingResult> PingResults { get; set; } = [];

    public ICollection<Incident> Incidents { get; set; } = [];

    public double UptimePercent { get; set; } = 100;
}
