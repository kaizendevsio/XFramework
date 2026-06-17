namespace Notifications.Domain.Shared.Contracts;

public static class NotificationPreferenceDefaults
{
    public const NotificationDeliveryChannel EnabledChannels = NotificationDeliveryChannel.InApp;

    public static NotificationDeliveryChannel Normalize(NotificationDeliveryChannel channels) =>
        channels == NotificationDeliveryChannel.None ? EnabledChannels : channels & NotificationDeliveryChannel.All;
}
