using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

using TRequest = CancelReservationRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CancelReservationRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ReservationId { get; init; }
    public string? Reason { get; init; }
}
