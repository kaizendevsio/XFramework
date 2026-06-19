using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.MovementLedger;

public static class MovementLedgerReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/movement-ledger", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<MovementLedgerReportRow>>> Handle(
        GetMovementLedgerReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetMovementLedgerAsync(request, ct);
}
