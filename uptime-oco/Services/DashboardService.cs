using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

public class DashboardService(UptimeOcoContext context) : IDashboardService
{
    public async Task<ServiceResult<DashboardViewModel>> GetDashboardDataAsync()
    {
        var monitors = await context.Monitors
            .AsNoTracking()
            .ToListAsync();

        if (monitors.Count == 0)
        {
            return ServiceResult<DashboardViewModel>.Success(new DashboardViewModel());
        }

        var monitorIds = monitors.Select(m => m.Id).ToList();

        var recentPings = await context.PingResults
            .AsNoTracking()
            .Where(p => monitorIds.Contains(p.MonitorId))
            .OrderByDescending(p => p.CheckedAt)
            .Take(500)
            .ToListAsync();

        var openIncidents = await context.Incidents
            .AsNoTracking()
            .Where(i => i.ResolvedAt == null && monitorIds.Contains(i.MonitorId))
            .Select(i => i.MonitorId)
            .ToListAsync();

        var monitorStatuses = monitors.Select(m =>
        {
            var lastPing = recentPings.FirstOrDefault(p => p.MonitorId == m.Id);
            var hasOpenIncident = openIncidents.Contains(m.Id);
            var isUp = lastPing is not null && lastPing.IsSuccess && !hasOpenIncident;
            var monitorPings = recentPings.Where(p => p.MonitorId == m.Id).ToList();
            var monitorTotal = monitorPings.Count;
            var monitorSuccess = monitorPings.Count(p => p.IsSuccess);
            var monitorUptime = monitorTotal > 0
                ? Math.Round((double)monitorSuccess / monitorTotal * 100, 2)
                : 100;

            return new MonitorStatusDto
            {
                Id = m.Id,
                Name = m.Name,
                Url = m.Url,
                IntervalSeconds = m.IntervalSeconds,
                IsUp = isUp,
                IsActive = m.IsActive,
                LastCheckAt = m.LastCheckAt,
                LastResponseTimeMs = lastPing?.ResponseTimeMs,
                ConsecutiveFailures = m.ConsecutiveFailures,
                UptimePercent = monitorUptime
            };
        }).ToList();

        var totalPings = recentPings.Count;
        var successPings = recentPings.Count(p => p.IsSuccess);
        var uptimePercent = totalPings > 0
            ? Math.Round((double)successPings / totalPings * 100, 2)
            : 100;

        var avgResponse = recentPings
            .Where(p => p.ResponseTimeMs.HasValue)
            .Select(p => p.ResponseTimeMs!.Value)
            .DefaultIfEmpty(0)
            .Average();

        var responseTimeSeries = recentPings
            .Where(p => p.ResponseTimeMs.HasValue)
            .OrderBy(p => p.CheckedAt)
            .Select(p => new ResponseTimePoint
            {
                MonitorId = p.MonitorId,
                CheckedAt = p.CheckedAt,
                ResponseTimeMs = p.ResponseTimeMs!.Value,
                MonitorName = monitors.First(m => m.Id == p.MonitorId).Name
            })
            .ToList();

        var statusCodeDist = recentPings
            .Where(p => p.HttpStatusCode.HasValue)
            .GroupBy(p => p.HttpStatusCode!.Value)
            .Select(g => new StatusCodeCount
            {
                StatusCode = g.Key,
                Count = g.Count(),
                Monitors = g
                    .GroupBy(p => monitors.First(m => m.Id == p.MonitorId).Name)
                    .Select(mg => new StatusCodeMonitorCount { MonitorName = mg.Key, Count = mg.Count() })
                    .OrderByDescending(mg => mg.Count)
                    .ToList()
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        var vm = new DashboardViewModel
        {
            TotalMonitors = monitors.Count,
            MonitorsUp = monitorStatuses.Count(m => m.IsUp),
            MonitorsDown = monitorStatuses.Count(m => !m.IsUp),
            OverallUptimePercent = uptimePercent,
            AvgResponseTimeMs = Math.Round(avgResponse, 1),
            ResponseTimeSeries = responseTimeSeries,
            StatusCodeDistribution = statusCodeDist,
            MonitorStatuses = monitorStatuses
        };

        return ServiceResult<DashboardViewModel>.Success(vm);
    }
}
