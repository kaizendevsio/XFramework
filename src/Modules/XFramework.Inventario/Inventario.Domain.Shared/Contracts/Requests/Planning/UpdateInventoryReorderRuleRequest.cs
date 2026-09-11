using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

[MemoryPackable]
public partial record UpdateInventoryReorderRuleRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<UpdateInventoryReorderRuleRequest, CmdResponse>
{
    public Guid Id { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public decimal MinimumQuantity { get; init; }
    public decimal? MaximumQuantity { get; init; }
    public decimal ReorderPoint { get; init; }
    public decimal ReorderQuantity { get; init; }
    public string? PreferredSupplier { get; init; }
    public bool IsActive { get; init; }
}
