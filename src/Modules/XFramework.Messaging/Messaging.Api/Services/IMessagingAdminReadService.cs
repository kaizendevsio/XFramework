using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Messaging.Api.Services;

public interface IMessagingAdminReadService
{
    Task<Result<MessagingAdminUsersResponse>> QueryUsersAsync(
        QueryMessagingAdminUsersRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingAdminUserDetailResponse>> GetUserDetailAsync(
        GetMessagingAdminUserDetailRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingAdminThreadsResponse>> QueryThreadsAsync(
        QueryMessagingAdminThreadsRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingAdminThreadDetailResponse>> GetThreadDetailAsync(
        GetMessagingAdminThreadDetailRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingAdminOperationsResponse>> GetOperationsAsync(
        GetMessagingAdminOperationsRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingAdminModerationResponse>> GetModerationAsync(
        GetMessagingAdminModerationRequest request,
        CancellationToken ct = default);
}
