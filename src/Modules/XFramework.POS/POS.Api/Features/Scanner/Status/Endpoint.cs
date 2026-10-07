using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.ServiceIdentity;

namespace POS.Api.Features.Scanner.Status;

public static class GetPosScannerStatusEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/sales/scanner/status", Tags = ["POS Scanner"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosScannerPhoneResponse>> Handle(GetPosScannerStatusRequest request, PosScannerService service, CancellationToken ct) =>
        service.StatusAsync(request, ct);
}
