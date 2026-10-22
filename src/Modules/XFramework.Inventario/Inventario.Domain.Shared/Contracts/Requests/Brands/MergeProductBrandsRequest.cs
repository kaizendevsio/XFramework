using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;

[MemoryPackable]
public partial record MergeProductBrandsRequest : RequestBase,
    IQuery<QueryResponse<ProductBrandMergePreview>>,
    IBoltRequest<MergeProductBrandsRequest, QueryResponse<ProductBrandMergePreview>>
{
    public Guid CategoryId { get; init; }
    public string SourceBrand { get; init; } = string.Empty;
    public string TargetBrand { get; init; } = string.Empty;
    public bool Confirmed { get; init; }
    public int ExpectedProductCount { get; init; }
}

