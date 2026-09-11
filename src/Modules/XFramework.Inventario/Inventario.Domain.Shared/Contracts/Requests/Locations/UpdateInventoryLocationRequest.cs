using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

[MemoryPackable]
public partial record UpdateInventoryLocationRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<UpdateInventoryLocationRequest, CmdResponse>
{
    public Guid Id { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public InventoryLocationType LocationType { get; init; }
    public bool IsPickable { get; init; }
}
