using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

using TRequest = GetSellableProductRequest;
using TResponse = QueryResponse<SellableProductDetail>;

[MemoryPackable]
public partial record GetSellableProductRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
}
