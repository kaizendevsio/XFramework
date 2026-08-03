namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CheckCredentialCapabilityRequest : RequestBase,
    IQuery<QueryResponse<CredentialCapabilityCheckResponse>>,
    IBoltRequest<CheckCredentialCapabilityRequest, QueryResponse<CredentialCapabilityCheckResponse>>
{
    public Guid CredentialId { get; set; }
    public string? ModuleKey { get; set; }
    public string? SubFeatureKey { get; set; }
    public string? CapabilityKey { get; set; }
}

[MemoryPackable]
public partial record GetEffectiveCredentialCapabilitiesRequest : RequestBase,
    IQuery<QueryResponse<EffectiveCredentialCapabilitiesResponse>>,
    IBoltRequest<GetEffectiveCredentialCapabilitiesRequest, QueryResponse<EffectiveCredentialCapabilitiesResponse>>
{
    public Guid CredentialId { get; set; }
}

[MemoryPackable]
public partial record GetTenantAuthorizationPolicyRequest : RequestBase,
    IQuery<QueryResponse<TenantAuthorizationPolicyResponse>>,
    IBoltRequest<GetTenantAuthorizationPolicyRequest, QueryResponse<TenantAuthorizationPolicyResponse>>
{
    public Guid TenantId { get; set; }
}

[MemoryPackable]
public partial record UpdateTenantAuthorizationPolicyRequest : RequestBase,
    ICommand<QueryResponse<TenantAuthorizationPolicyResponse>>,
    IBoltRequest<UpdateTenantAuthorizationPolicyRequest, QueryResponse<TenantAuthorizationPolicyResponse>>
{
    public Guid TenantId { get; set; }
    public MissingPermissionBehavior MissingPermissionBehavior { get; set; } = MissingPermissionBehavior.Deny;
    public Guid ExpectedConcurrencyStamp { get; set; }
}

[MemoryPackable]
public partial record GetRoleTypePermissionsRequest : RequestBase,
    IQuery<QueryResponse<RoleTypePermissionsResponse>>,
    IBoltRequest<GetRoleTypePermissionsRequest, QueryResponse<RoleTypePermissionsResponse>>
{
    public Guid RoleTypeId { get; set; }
}

[MemoryPackable]
public partial record SetRoleTypePermissionsRequest : RequestBase,
    ICommand<QueryResponse<RoleTypePermissionsResponse>>,
    IBoltRequest<SetRoleTypePermissionsRequest, QueryResponse<RoleTypePermissionsResponse>>
{
    public Guid RoleTypeId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
    public List<CapabilityPermissionDto> Permissions { get; set; } = [];
}

[MemoryPackable]
public partial record GetCredentialRolePermissionOverridesRequest : RequestBase,
    IQuery<QueryResponse<CredentialRolePermissionOverridesResponse>>,
    IBoltRequest<GetCredentialRolePermissionOverridesRequest, QueryResponse<CredentialRolePermissionOverridesResponse>>
{
    public Guid IdentityRoleId { get; set; }
}

[MemoryPackable]
public partial record SetCredentialRolePermissionOverridesRequest : RequestBase,
    ICommand<QueryResponse<CredentialRolePermissionOverridesResponse>>,
    IBoltRequest<SetCredentialRolePermissionOverridesRequest, QueryResponse<CredentialRolePermissionOverridesResponse>>
{
    public Guid IdentityRoleId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
    public List<CapabilityPermissionDto> Overrides { get; set; } = [];
}

[MemoryPackable]
public partial record AssignCredentialRoleRequest : RequestBase,
    ICommand<QueryResponse<AssignedCredentialRoleResponse>>,
    IBoltRequest<AssignCredentialRoleRequest, QueryResponse<AssignedCredentialRoleResponse>>
{
    public Guid CredentialId { get; set; }
    public Guid RoleTypeId { get; set; }
    public DateTime RoleExpiration { get; set; }
}

[MemoryPackable]
public partial record RemoveCredentialRoleRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<RemoveCredentialRoleRequest, CmdResponse>
{
    public Guid IdentityRoleId { get; set; }
}
