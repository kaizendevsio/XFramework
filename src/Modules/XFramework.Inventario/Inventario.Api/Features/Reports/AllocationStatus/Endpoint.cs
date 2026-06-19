using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Reports.AllocationStatus;

public static class AllocationStatusReportEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reports/reservation-allocations", Tags = ["Inventario Reports"])]
    public static async Task<Result<List<ReservationAllocationStatusReportRow>>> Handle(
        GetReservationAllocationStatusReportRequest request,
        InventoryReportingService reportingService,
        CancellationToken ct) =>
        await reportingService.GetAllocationStatusAsync(request, ct);
}
