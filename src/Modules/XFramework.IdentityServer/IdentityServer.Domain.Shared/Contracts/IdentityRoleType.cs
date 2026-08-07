using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-role-types",
    RequireAuthorization = true,
    AuthorizationFeature = "identity.roles"
)]
public partial class IdentityRoleType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public short? RoleLevel { get; set; }

    [MemoryPackOrder(2)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(3)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual IdentityRoleTypeGroup? Group { get; set; }

    [MemoryPackOrder(5)]
    public virtual ICollection<IdentityRole> IdentityRoles { get; set; } = new List<IdentityRole>();

    [MemoryPackOrder(6)]
    public virtual ICollection<IdentityRoleTypeFeaturePermission> FeaturePermissions { get; set; } =
        new List<IdentityRoleTypeFeaturePermission>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetIdentityRoleTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? GroupId { get; set; }
}
