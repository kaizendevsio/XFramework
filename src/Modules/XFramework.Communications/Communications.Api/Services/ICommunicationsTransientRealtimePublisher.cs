using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Realtime;
using XFramework.Integration.Abstractions.Wrappers;

namespace Communications.Api.Services;

public interface ICommunicationsTransientRealtimePublisher
{
    Task PublishTypingAsync(CommunicationsTypingState state, CancellationToken ct = default);
    Task PublishPresenceAsync(CommunicationsPresenceState state, CancellationToken ct = default);
}

public sealed class CommunicationsTransientRealtimePublisher(
    IMessageBusWrapper bus) : ICommunicationsTransientRealtimePublisher
{
    public async Task PublishTypingAsync(CommunicationsTypingState state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await bus.PublishAsync(
            MessageRealtimeTopics.TypingEventName,
            MessageRealtimeTopics.ThreadTyping(state.TenantId, state.ThreadId),
            state,
            durable: false);
    }

    public async Task PublishPresenceAsync(CommunicationsPresenceState state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await bus.PublishAsync(
            MessageRealtimeTopics.PresenceEventName,
            MessageRealtimeTopics.Presence(state.TenantId),
            state,
            durable: false);
    }
}
