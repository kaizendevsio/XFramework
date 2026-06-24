namespace Messaging.Api.Services;

public interface IMessagingRealtimePublisher
{
    Task PublishAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default);
}
