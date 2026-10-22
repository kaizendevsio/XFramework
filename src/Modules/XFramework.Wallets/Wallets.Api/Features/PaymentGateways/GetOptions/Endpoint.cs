using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.PaymentGateways.GetOptions;

public static class GetWalletPaymentGatewayOptionsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/wallets/payment-gateway-options", Tags = ["Wallets"], Summary = "Get enabled payment gateway options", RequireAuthorization = true,
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<List<WalletPaymentGatewayOptionResponse>>> Handle(
        WalletPaymentGatewayOptionsRequest request, IWalletReportingService service, CancellationToken ct) =>
        service.GetPaymentGatewayOptionsAsync(request, ct);
}
