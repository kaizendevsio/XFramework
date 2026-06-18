using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

using TRequest = CreateInventoryLocationRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateInventoryLocationRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid WarehouseId { get; init; }
    public Guid? ParentLocationId { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public InventoryLocationType LocationType { get; init; } = InventoryLocationType.Bin;
    public bool IsPickable { get; init; } = true;
}
