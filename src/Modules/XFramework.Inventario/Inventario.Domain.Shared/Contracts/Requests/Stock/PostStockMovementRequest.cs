using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

using TRequest = PostStockMovementRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record PostStockMovementRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? LotId { get; init; }
    public Guid? DestinationWarehouseId { get; init; }
    public Guid? DestinationLocationId { get; init; }
    public InventoryMovementType MovementType { get; init; }
    public decimal Quantity { get; init; }
    public string? UnitOfMeasure { get; init; }
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? Reason { get; init; }
    public bool AllowNegativeStock { get; init; }
    public string? IdempotencyKey { get; init; }
}
