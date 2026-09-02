namespace uptime_oco;

public class NotificationOutbox
{
    public long Id { get; set; }

    public int IncidentId { get; set; }

    public Incident? Incident { get; set; }

    public int NotificationChannelId { get; set; }

    public NotificationChannel? NotificationChannel { get; set; }

    public required string Reason { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public bool IsResolved { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public DateTime? FailedAt { get; set; }

    public string? LastError { get; set; }
}
