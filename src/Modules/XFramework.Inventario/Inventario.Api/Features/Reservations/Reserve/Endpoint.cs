using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Reserve;

public static class ReserveInventoryEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reservations/reserve", Tags = ["Inventario Reservations"],
        Summary = "Reserve inventory",
        Description = "Creates an active reservation and updates reserved stock through the stock posting service.")]
    public static async Task<Result<Reservation>> Handle(
        ReserveInventoryRequest request,
        ReservationService reservationService,
        CancellationToken ct)
    {
        return await reservationService.ReserveAsync(request, ct);
    }
}
