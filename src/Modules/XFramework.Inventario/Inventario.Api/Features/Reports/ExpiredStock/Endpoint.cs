using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.ExpiredStock;

public static class ExpiredStockReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/expired-stock", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<NearExpiryStockReportRow>>> Handle(
        GetExpiredStockReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetExpiredAsync(request, ct);
}
