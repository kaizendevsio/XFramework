using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

using TRequest = SearchSellableProductsRequest;
using TResponse = QueryResponse<List<SellableProductCatalogItem>>;

[MemoryPackable]
public partial record SearchSellableProductsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsAvailable { get; init; } = true;
    public bool IncludeBaseProducts { get; init; } = true;
    public bool IncludeVariants { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
