using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

using TRequest = CreateInventoryReorderRuleRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateInventoryReorderRuleRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public decimal MinimumQuantity { get; init; }
    public decimal? MaximumQuantity { get; init; }
    public decimal ReorderPoint { get; init; }
    public decimal ReorderQuantity { get; init; }
    public string? PreferredSupplier { get; init; }
    public bool IsActive { get; init; } = true;
}
