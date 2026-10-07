using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;

[MemoryPackable]
public partial record GetInventarioSetupRequest : RequestBase,
    IQuery<QueryResponse<InventarioSetupResponse>>,
    IBoltRequest<GetInventarioSetupRequest, QueryResponse<InventarioSetupResponse>>;
