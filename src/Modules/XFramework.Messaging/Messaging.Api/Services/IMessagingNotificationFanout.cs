namespace Messaging.Api.Services;

public interface IMessagingNotificationFanout
{
    Task CreateNotificationsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default);
}
