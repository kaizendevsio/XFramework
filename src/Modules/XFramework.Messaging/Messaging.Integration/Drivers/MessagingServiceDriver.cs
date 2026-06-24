using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Realtime;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Messaging.Integration.Drivers;

public interface IMessagingServiceWrapper : IServiceWrapper
{
    Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);
    Task<QueryResponse<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        GetMessagingSettingsRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        UpdateMessagingSettingsRequest request,
        CancellationToken ct = default);
    Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeUserMessagingEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default);
    Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default);
}

public sealed record MessagingServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), IMessagingServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "XFramework.Messaging".ToSha256();
    }

    public async Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public Task<QueryResponse<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        GetMessagingSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingSettingsRequest, MessagingSettingsResponse>(request);
    }

    public Task<CmdResponse<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        UpdateMessagingSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateMessagingSettingsRequest, MessagingSettingsResponse>(request);
    }

    public Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"messaging:{tenantId:N}:{credentialId:N}:thread:{threadId:N}";
        return Bus.SubscribeDurableAsync<MessagingRealtimeEvent>(
            topic,
            subscriberId,
            evt => evt.ThreadId == threadId ? handler(evt) : Task.CompletedTask,
            ct);
    }

    public Task SubscribeUserMessagingEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"messaging:{tenantId:N}:{credentialId:N}:user";
        return Bus.SubscribeDurableAsync(topic, subscriberId, handler, ct);
    }

    public Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.TypingEventName,
            MessageRealtimeTopics.ThreadTyping(state.TenantId, state.ThreadId),
            state,
            durable: false);

    public Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.PresenceEventName,
            MessageRealtimeTopics.Presence(state.TenantId),
            state,
            durable: false);

    private IMessageBusWrapper Bus =>
        MessageBusDriver ?? throw new InvalidOperationException(
            $"{nameof(MessagingServiceWrapper)} cannot use realtime helpers without an {nameof(IMessageBusWrapper)}.");
}

public static class MessagingServiceWrapperExtensions
{
    public static void AddMessagingWrapperServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
    }
}
