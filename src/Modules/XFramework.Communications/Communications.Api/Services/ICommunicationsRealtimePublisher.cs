namespace Communications.Api.Services;

public interface ICommunicationsRealtimePublisher
{
    Task PublishAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default);
}
