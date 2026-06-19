using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.GetAllocations;

public static class GetReservationAllocationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/allocations", Tags = ["Inventario Reservations"],
        Summary = "Get reservation allocations",
        Description = "Gets reservation allocation rows for the authenticated tenant.")]
    public static async Task<Result<List<ReservationAllocation>>> Handle(
        GetReservationAllocationsRequest request,
        InventoryAllocationService allocationService,
        CancellationToken ct)
    {
        return await allocationService.GetAllocationsAsync(request, ct);
    }
}
