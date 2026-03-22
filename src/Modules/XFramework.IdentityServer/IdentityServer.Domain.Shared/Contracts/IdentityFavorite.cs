using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/identity-favorites",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "identity-favorites"
)]
public partial class IdentityFavorite : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? FavoriteTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? Data { get; set; }


    [MemoryPackOrder(3)]
    public virtual RegistryFavoriteType? FavoriteType { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}

public class CreateIdentityFavoriteRequest
{
    public Guid? FavoriteTypeId { get; set; }
    public Guid CredentialId { get; set; }
    public string? Data { get; set; }
}

public class UpdateIdentityFavoriteRequest
{
    public Guid? FavoriteTypeId { get; set; }
    public Guid CredentialId { get; set; }
    public string? Data { get; set; }
}

public class GetIdentityFavoriteListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? FavoriteTypeId { get; set; }
    public Guid? CredentialId { get; set; }
}
