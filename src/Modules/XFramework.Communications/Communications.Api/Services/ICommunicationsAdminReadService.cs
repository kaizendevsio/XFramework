using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public interface ICommunicationsAdminReadService
{
    Task<Result<CommunicationsAdminUsersResponse>> QueryUsersAsync(
        QueryCommunicationsAdminUsersRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsAdminUserDetailResponse>> GetUserDetailAsync(
        GetCommunicationsAdminUserDetailRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsAdminThreadsResponse>> QueryThreadsAsync(
        QueryCommunicationsAdminThreadsRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsAdminThreadDetailResponse>> GetThreadDetailAsync(
        GetCommunicationsAdminThreadDetailRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsAdminOperationsResponse>> GetOperationsAsync(
        GetCommunicationsAdminOperationsRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsAdminModerationResponse>> GetModerationAsync(
        GetCommunicationsAdminModerationRequest request,
        CancellationToken ct = default);
}
