using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-role-type-groups",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "identity-role-type-groups"
)]
public partial class IdentityRoleTypeGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Description { get; set; } = null!;


    [MemoryPackOrder(2)]
    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; set; } = new List<IdentityRoleType>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetIdentityRoleTypeGroupListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
