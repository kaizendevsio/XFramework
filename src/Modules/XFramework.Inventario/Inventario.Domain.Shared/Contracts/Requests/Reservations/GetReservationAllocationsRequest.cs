using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

using TRequest = GetReservationAllocationsRequest;
using TResponse = QueryResponse<List<ReservationAllocation>>;

[MemoryPackable]
public partial record GetReservationAllocationsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ReservationId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? LotId { get; init; }
    public ReservationAllocationStatus? Status { get; init; }
}
