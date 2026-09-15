namespace Notifications.Domain.Shared.Contracts;

public static class NotificationPreferenceDefaults
{
    // Push is on by default because it is the only channel that reaches a closed PWA, and it
    // still delivers nothing until the person grants permission and registers a device.
    public const NotificationDeliveryChannel EnabledChannels =
        NotificationDeliveryChannel.InApp | NotificationDeliveryChannel.Push;

    public static NotificationDeliveryChannel Normalize(NotificationDeliveryChannel channels) =>
        channels == NotificationDeliveryChannel.None ? EnabledChannels : channels & NotificationDeliveryChannel.All;
}
