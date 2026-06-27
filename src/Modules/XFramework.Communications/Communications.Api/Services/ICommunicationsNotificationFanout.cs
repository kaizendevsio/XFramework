namespace Communications.Api.Services;

public interface ICommunicationsNotificationFanout
{
    Task CreateNotificationsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default);
}
