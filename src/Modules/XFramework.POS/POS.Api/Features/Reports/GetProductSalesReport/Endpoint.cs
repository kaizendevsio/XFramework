using IdentityServer.Domain.Shared.Contracts;
using POS.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace POS.Api.Features.Reports.GetProductSalesReport;

public static class GetPosProductSalesReportEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReportingView])]
    [MapGet("/api/pos/reports/products", Tags = ["POS Reporting"], Summary = "Get paged sold-product quantities and revenue before receipt discounts, tax and refunds",
        Capability = IdentityAuthorizationConstants.View, TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReportingView])]
    public static Task<Result<PosProductSalesReportResponse>> Handle(
        GetPosProductSalesReportRequest request, PosReportingService service, CancellationToken ct) =>
        service.GetProductSalesReportAsync(request, ct);
}
