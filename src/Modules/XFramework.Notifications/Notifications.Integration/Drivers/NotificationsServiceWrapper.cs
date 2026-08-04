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
}

public sealed record NotificationsServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), INotificationsServiceWrapper
{
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
}

public static class NotificationsServiceWrapperExtensions
{
    public static void AddNotificationsWrapperServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationsServiceWrapper, NotificationsServiceWrapper>();
    }
}
