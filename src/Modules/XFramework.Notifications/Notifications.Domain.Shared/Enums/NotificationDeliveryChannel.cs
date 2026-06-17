namespace Notifications.Domain.Shared.Enums;

[Flags]
public enum NotificationDeliveryChannel
{
    None = 0,
    InApp = 1,
    Email = 2,
    Sms = 4,
    Push = 8,
    Webhook = 16,
    All = InApp | Email | Sms | Push | Webhook
}
