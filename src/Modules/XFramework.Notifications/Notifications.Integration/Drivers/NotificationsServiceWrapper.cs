using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Notifications.Integration.Drivers;

public interface INotificationsServiceWrapper : IServiceWrapper
{
    Task<CmdResponse> SetPushPresence(SetPushPresenceRequest request, CancellationToken ct = default);
    Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(
        CreateNotificationRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(
        GetNotificationInboxRequest request,
        CancellationToken ct = default);

    Task<CmdResponse> MarkNotificationRead(
        MarkNotificationReadRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
        RecordNotificationDeliveryStatusRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<PushConfigurationResponse>> GetPushConfiguration(
        GetPushConfigurationRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<PushSubscriptionResponse>> RegisterPushSubscription(
        RegisterPushSubscriptionRequest request,
        CancellationToken ct = default);

    Task<CmdResponse> RemovePushSubscription(
        RemovePushSubscriptionRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<SendDirectPushResponse>> SendDirectPush(
        SendDirectPushRequest request,
        CancellationToken ct = default);
}

public sealed record NotificationsServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), INotificationsServiceWrapper
{
    public Task<CmdResponse> SetPushPresence(SetPushPresenceRequest request, CancellationToken ct = default) => SendVoidAsync(request, ct);

    public override void Initialize()
    {
        TargetClient = "XFramework.Notifications".ToSha256();
    }

    public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(
        CreateNotificationRequest request,
        CancellationToken ct = default) =>
        SendAsync<CreateNotificationRequest, NotificationInboxItemResponse>(request, ct);

    public Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(
        GetNotificationInboxRequest request,
        CancellationToken ct = default) =>
        SendAsync<GetNotificationInboxRequest, GetNotificationInboxResponse>(request, ct);

    public Task<CmdResponse> MarkNotificationRead(
        MarkNotificationReadRequest request,
        CancellationToken ct = default) =>
        SendVoidAsync(request, ct);

    public Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default) =>
        SendAsync<UpdateNotificationPreferencesRequest, NotificationPreferencesResponse>(request, ct);

    public Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
        RecordNotificationDeliveryStatusRequest request,
        CancellationToken ct = default) =>
        SendAsync<RecordNotificationDeliveryStatusRequest, NotificationDeliveryStatusResponse>(request, ct);

    public Task<QueryResponse<PushConfigurationResponse>> GetPushConfiguration(
        GetPushConfigurationRequest request,
        CancellationToken ct = default) =>
        SendAsync<GetPushConfigurationRequest, PushConfigurationResponse>(request, ct);

    public Task<QueryResponse<PushSubscriptionResponse>> RegisterPushSubscription(
        RegisterPushSubscriptionRequest request,
        CancellationToken ct = default) =>
        SendAsync<RegisterPushSubscriptionRequest, PushSubscriptionResponse>(request, ct);

    public Task<CmdResponse> RemovePushSubscription(
        RemovePushSubscriptionRequest request,
        CancellationToken ct = default) =>
        SendVoidAsync(request, ct);

    public Task<QueryResponse<SendDirectPushResponse>> SendDirectPush(
        SendDirectPushRequest request,
        CancellationToken ct = default) =>
        SendAsync<SendDirectPushRequest, SendDirectPushResponse>(request, ct);
}

public static class NotificationsServiceWrapperExtensions
{
    public static void AddNotificationsWrapperServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationsServiceWrapper, NotificationsServiceWrapper>();
    }
}
