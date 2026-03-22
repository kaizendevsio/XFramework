using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Create | EndpointActions.Get | EndpointActions.GetList | EndpointActions.Delete,
    RoutePrefix = "api/identity-roles",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "identity-roles"
)]
public partial class IdentityRole : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? TypeId { get; set; }

    [MemoryPackOrder(2)]
    public DateTime RoleExpiration { get; set; }
    
    [MemoryPackOrder(4)]
    public virtual IdentityRoleType? Type { get; set; }

    [MemoryPackOrder(5)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}

public class CreateIdentityRoleRequest
{
    public Guid CredentialId { get; set; }
    public Guid? TypeId { get; set; }
    public DateTime RoleExpiration { get; set; }
}

public class GetIdentityRoleListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? TypeId { get; set; }
}
