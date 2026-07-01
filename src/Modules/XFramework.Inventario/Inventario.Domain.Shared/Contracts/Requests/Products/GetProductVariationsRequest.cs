using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

using TRequest = GetProductVariationsRequest;
using TResponse = QueryResponse<List<SellableProductVariationItem>>;

[MemoryPackable]
public partial record GetProductVariationsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
}
