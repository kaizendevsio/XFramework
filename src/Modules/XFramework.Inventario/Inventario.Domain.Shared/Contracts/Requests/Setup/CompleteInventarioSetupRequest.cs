using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;

[MemoryPackable]
public partial record CompleteInventarioSetupRequest : RequestBase,
    IQuery<QueryResponse<InventarioSetupResponse>>,
    IBoltRequest<CompleteInventarioSetupRequest, QueryResponse<InventarioSetupResponse>>
{
    public Guid CompletionRequestId { get; init; }
    public Guid? ExpectedConcurrencyStamp { get; init; }
    public InventarioSetupMode Mode { get; init; }
    public Guid? ExistingWarehouseId { get; init; }
    public Guid? ExistingLocationId { get; init; }
    public CreateWarehouseRequest? Warehouse { get; init; }
    public CreateInventoryLocationRequest? Location { get; init; }
    public int LowStockThreshold { get; init; } = 5;
    public string DefaultCurrency { get; init; } = "PHP";
}
