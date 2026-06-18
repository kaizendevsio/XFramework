using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Expire;

public static class ExpireReservationsEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reservations/expire", Tags = ["Inventario Reservations"],
        Summary = "Expire reservations",
        Description = "Expires active reservations whose expiration time is due and releases their reserved quantity.")]
    public static async Task<Result<int>> Handle(
        ExpireReservationsRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.ExpireAsync(request, ct);
    }
}
