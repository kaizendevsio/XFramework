using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

using TRequest = GetInventoryLotsRequest;
using TResponse = QueryResponse<List<InventoryLot>>;

[MemoryPackable]
public partial record GetInventoryLotsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public InventoryLotStatus? Status { get; init; }
    public bool IncludeExpired { get; init; }
}
