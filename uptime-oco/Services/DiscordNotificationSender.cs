namespace uptime_oco;

public sealed class DiscordNotificationSender(
    IHttpClientFactory httpClientFactory) : HttpNotificationSender(httpClientFactory)
{
    public override NotificationType Type => NotificationType.Discord;

    protected override object CreatePayload(NotificationMessage message)
    {
        return new { content = message.Content };
    }
}
