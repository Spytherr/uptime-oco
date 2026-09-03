namespace uptime_oco;

public sealed class WebhookNotificationSender(
    IHttpClientFactory httpClientFactory) : HttpNotificationSender(httpClientFactory)
{
    public override NotificationType Type => NotificationType.Webhook;

    protected override object CreatePayload(NotificationMessage message)
    {
        return new { text = message.Content, title = message.Title };
    }
}
