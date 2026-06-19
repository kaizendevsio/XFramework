using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.LowStock;

public static class LowStockReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/low-stock", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<LowStockReportRow>>> Handle(
        GetLowStockReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetLowStockAsync(request, ct);
}
