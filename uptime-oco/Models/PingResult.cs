namespace uptime_oco;

public class PingResult
{
    public int Id { get; set; }

    public int MonitorId { get; set; }

    public Monitor? Monitor { get; set; }

    public int? ResponseTimeMs { get; set; }

    public int? HttpStatusCode { get; set; }

    public bool IsSuccess { get; set; }

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
