using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Cancel;

public static class CancelReservationEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reservations/cancel", Tags = ["Inventario Reservations"],
        Summary = "Cancel reservation",
        Description = "Cancels an active reservation and releases its reserved quantity.")]
    public static async Task<Result<Reservation>> Handle(
        CancelReservationRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.CancelAsync(request, ct);
    }
}
