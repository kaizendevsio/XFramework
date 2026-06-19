using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

using TRequest = CreateInventoryLotRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateInventoryLotRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
    public string? LotNumber { get; init; }
    public string? SupplierReference { get; init; }
    public string? SourceReferenceType { get; init; }
    public Guid? SourceReferenceId { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public DateTime? ManufacturedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public decimal? UnitCost { get; init; }
    public InventoryLotStatus Status { get; init; } = InventoryLotStatus.Available;
}
