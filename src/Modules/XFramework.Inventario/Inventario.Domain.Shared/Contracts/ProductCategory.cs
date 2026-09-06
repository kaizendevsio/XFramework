using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[AllowRemoteDataContextQuery]
[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/product-categories",
    RequireAuthorization = true,
    AuthorizationFeature = "inventario.catalog",
    TenantAccessMode = GeneratedTenantAccessMode.DelegatedTenant,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-product-categories"
)]
public partial class ProductCategory : BaseModel
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public string? Description { get; set; }
    [MemoryPackOrder(2)]
    public List<Product>? Products { get; set; } = new();
}
