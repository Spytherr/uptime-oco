using Microsoft.AspNetCore.SignalR;

namespace uptime_oco;

public class MonitorHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}
