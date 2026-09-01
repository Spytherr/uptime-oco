namespace uptime_oco;

public static class MonitorStatusCalculator
{
    public static MonitorStatus Calculate(
        Monitor monitor,
        bool hasCheckResult,
        bool hasOpenIncident = false)
    {
        if (!monitor.IsActive)
        {
            return MonitorStatus.Paused;
        }

        var retryThreshold = Math.Max(monitor.RetryThreshold, 1);
        if (hasOpenIncident || monitor.ConsecutiveFailures >= retryThreshold)
        {
            return MonitorStatus.Down;
        }

        if (!hasCheckResult && monitor.LastCheckAt is null)
        {
            return MonitorStatus.Unknown;
        }

        return MonitorStatus.Up;
    }
}
