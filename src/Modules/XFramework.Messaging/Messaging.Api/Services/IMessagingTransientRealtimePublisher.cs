using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Realtime;
using XFramework.Integration.Abstractions.Wrappers;

namespace Messaging.Api.Services;

public interface IMessagingTransientRealtimePublisher
{
    Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default);
    Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default);
}

public sealed class MessagingTransientRealtimePublisher(
    IMessageBusWrapper bus) : IMessagingTransientRealtimePublisher
{
    public async Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await bus.PublishAsync(
            MessageRealtimeTopics.TypingEventName,
            MessageRealtimeTopics.ThreadTyping(state.TenantId, state.ThreadId),
            state,
            durable: false);
    }

    public async Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await bus.PublishAsync(
            MessageRealtimeTopics.PresenceEventName,
            MessageRealtimeTopics.Presence(state.TenantId),
            state,
            durable: false);
    }
}
