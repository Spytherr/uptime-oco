namespace uptime_oco;

public sealed class MonitorDetailsViewModel
{
    public required Monitor Monitor { get; init; }

    public List<ResponseTimePoint> ResponseTimeSeries { get; init; } = [];
}
