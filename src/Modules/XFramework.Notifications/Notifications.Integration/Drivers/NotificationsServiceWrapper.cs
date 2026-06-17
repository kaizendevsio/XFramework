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
    Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(CreateNotificationRequest request);
    Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(GetNotificationInboxRequest request);
    Task<CmdResponse> MarkNotificationRead(MarkNotificationReadRequest request);
    Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(UpdateNotificationPreferencesRequest request);
    Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
        RecordNotificationDeliveryStatusRequest request);
}

public sealed record NotificationsServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), INotificationsServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "Notifications".ToSha256();
    }

    public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(CreateNotificationRequest request) =>
        SendAsync<CreateNotificationRequest, NotificationInboxItemResponse>(request);

    public Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(GetNotificationInboxRequest request) =>
        SendAsync<GetNotificationInboxRequest, GetNotificationInboxResponse>(request);

    public Task<CmdResponse> MarkNotificationRead(MarkNotificationReadRequest request) =>
        SendVoidAsync(request);

    public Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(
        UpdateNotificationPreferencesRequest request) =>
        SendAsync<UpdateNotificationPreferencesRequest, NotificationPreferencesResponse>(request);

    public Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
        RecordNotificationDeliveryStatusRequest request) =>
        SendAsync<RecordNotificationDeliveryStatusRequest, NotificationDeliveryStatusResponse>(request);
}

public static class NotificationsServiceWrapperExtensions
{
    public static void AddNotificationsWrapperServices(this IServiceCollection services)
    {
        services.AddSingleton<INotificationsServiceWrapper, NotificationsServiceWrapper>();
    }
}
