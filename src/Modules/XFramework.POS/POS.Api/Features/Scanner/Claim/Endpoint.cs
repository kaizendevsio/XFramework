using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.ServiceIdentity;

namespace POS.Api.Features.Scanner.Claim;

public static class ClaimPosScannerEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/sales/scanner/claim", Tags = ["POS Scanner"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosScannerPhoneResponse>> Handle(ClaimPosScannerPairingRequest request, PosScannerService service, CancellationToken ct) =>
        service.ClaimAsync(request, ct);
}
