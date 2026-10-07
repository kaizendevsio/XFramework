using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;

[MemoryPackable]
public partial record UpdateInventarioPreferencesRequest : RequestBase,
    IQuery<QueryResponse<InventarioSetupResponse>>,
    IBoltRequest<UpdateInventarioPreferencesRequest, QueryResponse<InventarioSetupResponse>>
{
    public Guid? ExpectedConcurrencyStamp { get; init; }
    public int LowStockThreshold { get; init; }
    public string DefaultCurrency { get; init; } = "PHP";
}
