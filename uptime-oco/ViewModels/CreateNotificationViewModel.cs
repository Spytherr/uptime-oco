namespace uptime_oco;

public class CreateNotificationViewModel
{
    public string Name { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Discord;
    public string Target { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
