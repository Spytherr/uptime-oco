namespace uptime_oco;

public class NotificationChannel
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public NotificationType Type { get; set; }

    public required string Target { get; set; }

    public string? ConfigJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }
}
