using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Fulfill;

public static class FulfillReservationEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reservations/fulfill", Tags = ["Inventario Reservations"],
        Summary = "Fulfill reservation",
        Description = "Fulfills an active reservation by releasing reserved stock and posting a shipment movement.")]
    public static async Task<Result<Reservation>> Handle(
        FulfillReservationRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.FulfillAsync(request, ct);
    }
}
