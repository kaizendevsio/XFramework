namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityRoleTypeFeaturePermission : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid RoleTypeId { get; set; }

    [MemoryPackOrder(1)]
    public string ModuleKey { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string SubFeatureKey { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string CapabilityKey { get; set; } = null!;

    [MemoryPackOrder(4)]
    public RoleCapabilityPermissionEffect Effect { get; set; } = RoleCapabilityPermissionEffect.Allow;

    [MemoryPackOrder(5)]
    public virtual IdentityRoleType RoleType { get; set; } = null!;

    [MemoryPackIgnore]
    public string FeatureKey => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);
}
