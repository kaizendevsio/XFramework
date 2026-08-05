using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Admin;

public static class QueryCommunicationsAdminUsersEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/communications/admin/users/query", Tags = ["Communications Admin"],
        Summary = "Query Communications admin users",
        Description = "Returns tenant-scoped Communications user diagnostics with privacy-safe message metadata.")]
    public static Task<Result<CommunicationsAdminUsersResponse>> Handle(
        QueryCommunicationsAdminUsersRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.QueryUsersAsync(request, ct);
}

public static class GetCommunicationsAdminUserDetailEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/admin/users/{credentialId:guid}", Tags = ["Communications Admin"],
        Summary = "Get Communications admin user detail",
        Description = "Returns tenant-scoped Communications activity grouped around one credential.")]
    public static Task<Result<CommunicationsAdminUserDetailResponse>> Handle(
        GetCommunicationsAdminUserDetailRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetUserDetailAsync(request, ct);
}

public static class QueryCommunicationsAdminThreadsEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/communications/admin/threads/query", Tags = ["Communications Admin"],
        Summary = "Query Communications admin threads",
        Description = "Returns tenant-scoped Communications thread diagnostics with short previews only.")]
    public static Task<Result<CommunicationsAdminThreadsResponse>> Handle(
        QueryCommunicationsAdminThreadsRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.QueryThreadsAsync(request, ct);
}

public static class GetCommunicationsAdminThreadDetailEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/admin/threads/{threadId:guid}", Tags = ["Communications Admin"],
        Summary = "Get Communications admin thread detail",
        Description = "Returns tenant-scoped thread members and short message previews.")]
    public static Task<Result<CommunicationsAdminThreadDetailResponse>> Handle(
        GetCommunicationsAdminThreadDetailRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetThreadDetailAsync(request, ct);
}

public static class GetCommunicationsAdminOperationsEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/admin/operations", Tags = ["Communications Admin"],
        Summary = "Get Communications admin operations state",
        Description = "Returns tenant-scoped outbox, invite, pin, and saved-message diagnostics.")]
    public static Task<Result<CommunicationsAdminOperationsResponse>> Handle(
        GetCommunicationsAdminOperationsRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetOperationsAsync(request, ct);
}

public static class GetCommunicationsAdminModerationEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsAdmin],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/admin/moderation", Tags = ["Communications Admin"],
        Summary = "Get Communications admin moderation state",
        Description = "Returns tenant-scoped moderation reports, blocks, and enforced policy state.")]
    public static Task<Result<CommunicationsAdminModerationResponse>> Handle(
        GetCommunicationsAdminModerationRequest request,
        ICommunicationsAdminReadService adminReadService,
        CancellationToken ct) =>
        adminReadService.GetModerationAsync(request, ct);
}
