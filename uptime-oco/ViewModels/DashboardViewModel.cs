namespace uptime_oco;

public class DashboardViewModel
{
    public int TotalMonitors { get; set; }
    public int MonitorsUp { get; set; }
    public int MonitorsDown { get; set; }
    public double OverallUptimePercent { get; set; }
    public double AvgResponseTimeMs { get; set; }
    public List<ResponseTimePoint> ResponseTimeSeries { get; set; } = [];
    public List<StatusCodeCount> StatusCodeDistribution { get; set; } = [];
    public List<MonitorStatusDto> MonitorStatuses { get; set; } = [];
}

public class ResponseTimePoint
{
    public DateTime CheckedAt { get; set; }
    public int ResponseTimeMs { get; set; }
    public string MonitorName { get; set; } = string.Empty;
}

public class StatusCodeCount
{
    public int StatusCode { get; set; }
    public int Count { get; set; }
    public List<StatusCodeMonitorCount> Monitors { get; set; } = [];
}

public class StatusCodeMonitorCount
{
    public string MonitorName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MonitorStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsUp { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public int? LastResponseTimeMs { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double UptimePercent { get; set; }
}
