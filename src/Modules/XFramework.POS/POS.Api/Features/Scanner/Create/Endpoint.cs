using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.ServiceIdentity;

namespace POS.Api.Features.Scanner.Create;

public static class CreatePosScannerEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/sales/scanner/create", Tags = ["POS Scanner"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosScannerPairingResponse>> Handle(CreatePosScannerPairingRequest request, PosScannerService service, CancellationToken ct) =>
        service.CreateAsync(request, ct);
}
