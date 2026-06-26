using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Admin;

public static class QueryMessagingAdminUsersEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/admin/users/query", Tags = ["Messaging Admin"],
        Summary = "Query Messaging admin users",
        Description = "Returns tenant-scoped Messaging user diagnostics with privacy-safe message metadata.")]
    public static Task<Result<MessagingAdminUsersResponse>> Handle(
        QueryMessagingAdminUsersRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.QueryUsersAsync(request, ct);
}

public static class GetMessagingAdminUserDetailEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/admin/users/{credentialId:guid}", Tags = ["Messaging Admin"],
        Summary = "Get Messaging admin user detail",
        Description = "Returns tenant-scoped Messaging activity grouped around one credential.")]
    public static Task<Result<MessagingAdminUserDetailResponse>> Handle(
        GetMessagingAdminUserDetailRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetUserDetailAsync(request, ct);
}

public static class QueryMessagingAdminThreadsEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/admin/threads/query", Tags = ["Messaging Admin"],
        Summary = "Query Messaging admin threads",
        Description = "Returns tenant-scoped Messaging thread diagnostics with short previews only.")]
    public static Task<Result<MessagingAdminThreadsResponse>> Handle(
        QueryMessagingAdminThreadsRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.QueryThreadsAsync(request, ct);
}

public static class GetMessagingAdminThreadDetailEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/admin/threads/{threadId:guid}", Tags = ["Messaging Admin"],
        Summary = "Get Messaging admin thread detail",
        Description = "Returns tenant-scoped thread members and short message previews.")]
    public static Task<Result<MessagingAdminThreadDetailResponse>> Handle(
        GetMessagingAdminThreadDetailRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetThreadDetailAsync(request, ct);
}

public static class GetMessagingAdminOperationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/admin/operations", Tags = ["Messaging Admin"],
        Summary = "Get Messaging admin operations state",
        Description = "Returns tenant-scoped outbox, invite, pin, and saved-message diagnostics.")]
    public static Task<Result<MessagingAdminOperationsResponse>> Handle(
        GetMessagingAdminOperationsRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetOperationsAsync(request, ct);
}

public static class GetMessagingAdminModerationEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/admin/moderation", Tags = ["Messaging Admin"],
        Summary = "Get Messaging admin moderation state",
        Description = "Returns tenant-scoped moderation reports, blocks, and enforced policy state.")]
    public static Task<Result<MessagingAdminModerationResponse>> Handle(
        GetMessagingAdminModerationRequest request,
        IMessagingAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetModerationAsync(request, ct);
}
