using Microsoft.AspNetCore.Identity;

namespace uptime_oco;

public class ApplicationUser : IdentityUser
{
    public ICollection<Monitor> Monitors { get; set; } = [];

    public ICollection<NotificationChannel> NotificationChannels { get; set; } = [];
}
