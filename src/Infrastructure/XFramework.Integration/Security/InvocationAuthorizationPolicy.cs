namespace XFramework.Integration.Security;

public enum ActorRequirement
{
    Required,
    Optional,
    None
}

public enum TenantAccessMode
{
    ActorTenant,
    DelegatedTenant,
    ServiceTargetTenant,
    Tenantless,
    PublicTenantLookup
}

public sealed record InvocationAuthorizationPolicy
{
    public ActorRequirement ActorRequirement { get; init; } = ActorRequirement.Required;
    public TenantAccessMode TenantAccessMode { get; init; } = TenantAccessMode.ActorTenant;
    public bool RequireServiceIdentity { get; init; } = true;
    public IReadOnlyCollection<string> RequiredServiceScopes { get; init; } = [];
    public IReadOnlyCollection<string> AllowedServiceCallers { get; init; } = [];
    public IReadOnlyCollection<string> RequiredActorRoles { get; init; } = [];
    public IReadOnlyCollection<string> RequiredActorCapabilities { get; init; } = [];
    public IReadOnlyCollection<string> RequiredCrossTenantActorCapabilities { get; init; } = [];
    public IReadOnlyDictionary<string, string> RequiredActorAttributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool AllowAnonymous { get; init; }
}
