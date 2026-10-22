using IdentityServer.Domain.Shared.Contracts;
using POS.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace POS.Api.Features.Reports.GetSalesReport;

public static class GetPosSalesReportEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReportingView])]
    [MapGet("/api/pos/reports/sales", Tags = ["POS Reporting"],
        Summary = "Get bounded POS sales report",
        Capability = IdentityAuthorizationConstants.View,
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReportingView])]
    public static Task<Result<PosSalesReportResponse>> Handle(
        GetPosSalesReportRequest request, PosReportingService service, CancellationToken ct) =>
        service.GetSalesReportAsync(request, ct);
}
