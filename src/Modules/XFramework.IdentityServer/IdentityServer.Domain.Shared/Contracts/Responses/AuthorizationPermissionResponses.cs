namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record TenantAuthorizationPolicyResponse
{
    public Guid TenantId { get; set; }
    public MissingPermissionBehavior MissingPermissionBehavior { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}

[MemoryPackable]
public partial record CapabilityPermissionDto
{
    public Guid? Id { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string SubFeatureKey { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public RoleCapabilityPermissionEffect Effect { get; set; }
    public bool IsEnabled { get; set; } = true;
}

[MemoryPackable]
public partial record RoleTypePermissionsResponse
{
    public Guid TenantId { get; set; }
    public Guid RoleTypeId { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public List<CapabilityPermissionDto> Permissions { get; set; } = [];
}

[MemoryPackable]
public partial record CredentialRolePermissionOverridesResponse
{
    public Guid TenantId { get; set; }
    public Guid IdentityRoleId { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public List<CapabilityPermissionDto> Overrides { get; set; } = [];
}

[MemoryPackable]
public partial record CredentialCapabilityCheckResponse
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string SubFeatureKey { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public string DecisionSource { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record EffectiveCredentialCapabilitiesResponse
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public List<CredentialCapabilityCheckResponse> Capabilities { get; set; } = [];
}
