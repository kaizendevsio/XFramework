using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.NearExpiry;

public static class NearExpiryReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/near-expiry", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<NearExpiryStockReportRow>>> Handle(
        GetNearExpiryStockReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetNearExpiryAsync(request, ct);
}
