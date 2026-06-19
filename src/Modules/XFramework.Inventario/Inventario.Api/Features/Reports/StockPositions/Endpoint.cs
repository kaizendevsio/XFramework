using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.StockPositions;

public static class StockPositionsReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/stock-positions", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<StockPositionReportRow>>> Handle(
        GetStockPositionReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetStockPositionsAsync(request, ct);
}
