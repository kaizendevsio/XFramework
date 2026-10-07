using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.ServiceIdentity;

namespace POS.Api.Features.Scanner.Send;

public static class SendPosScannerEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/sales/scanner/send", Tags = ["POS Scanner"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosScannerSendResponse>> Handle(SendPosScannerCodeRequest request, PosScannerService service, CancellationToken ct) =>
        service.SendAsync(request, ct);
}
