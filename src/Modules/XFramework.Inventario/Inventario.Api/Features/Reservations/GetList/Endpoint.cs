using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.GetList;

public static class GetReservationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reservations", Tags = ["Inventario Reservations"],
        Summary = "Get inventory reservations",
        Description = "Gets inventory reservations for the authenticated tenant.")]
    public static async Task<Result<List<Reservation>>> Handle(
        GetReservationsRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.GetReservationsAsync(request, ct);
    }
}
