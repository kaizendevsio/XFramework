using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

[MemoryPackable]
public partial record UpdateInventoryLotRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<UpdateInventoryLotRequest, CmdResponse>
{
    public Guid Id { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public string? SupplierReference { get; init; }
    public DateTime? ManufacturedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public InventoryLotStatus Status { get; init; }
}
