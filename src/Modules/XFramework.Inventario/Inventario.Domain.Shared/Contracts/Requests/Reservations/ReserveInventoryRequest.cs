using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

using TRequest = ReserveInventoryRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ReserveInventoryRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? LotId { get; init; }
    public decimal Quantity { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? UnitOfMeasure { get; init; }
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? Reason { get; init; }
    public bool AllowExpiredLotOverride { get; init; }
    public string? ExpiredLotOverrideReason { get; init; }
}
