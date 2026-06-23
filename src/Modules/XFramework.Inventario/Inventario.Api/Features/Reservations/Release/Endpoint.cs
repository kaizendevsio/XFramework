using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Release;

public static class ReleaseReservationEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reservations/release", Tags = ["Inventario Reservations"],
        Summary = "Release reservation",
        Description = "Releases an active reservation and returns reserved quantity to available stock.")]
    public static async Task<Result<Reservation>> Handle(
        ReleaseReservationRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.ReleaseAsync(request, ct);
    }
}
