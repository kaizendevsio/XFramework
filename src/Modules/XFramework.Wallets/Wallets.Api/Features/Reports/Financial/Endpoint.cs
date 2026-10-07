using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.Reports.Financial;

public static class GetWalletFinancialReportEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/wallets/reports/financial", Tags = ["WalletsReporting"], Summary = "Financial activity by currency", RequireAuthorization = true,
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<WalletFinancialReportResponse>> Handle(WalletFinancialReportRequest request, IWalletReportingService service, CancellationToken ct) => service.GetFinancialReportAsync(request, ct);
}
