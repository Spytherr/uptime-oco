using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace uptime_oco;

[Authorize]
public class MonitorHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}
