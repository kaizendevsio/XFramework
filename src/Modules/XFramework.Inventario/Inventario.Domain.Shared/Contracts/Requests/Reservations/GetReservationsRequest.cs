using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

using TRequest = GetReservationsRequest;
using TResponse = QueryResponse<List<Reservation>>;

[MemoryPackable]
public partial record GetReservationsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public ReservationStatus? Status { get; init; }
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
}
