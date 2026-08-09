namespace XFramework.Domain.Shared.Attributes;

public enum GeneratedActorRequirement
{
    Required,
    Optional,
    None
}

public enum GeneratedTenantAccessMode
{
    ActorTenant,
    DelegatedTenant,
    Tenantless
}

/// <summary>
/// Requires a validated actor attribute for selected generated entity operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequireGeneratedActorAttributeAttribute(
    string name,
    string value) : Attribute
{
    public string Name { get; } = name;
    public string Value { get; } = value;
    public EndpointActions Actions { get; set; } = EndpointActions.All;
}

/// <summary>
/// Explicitly permits service-only remote data-context access for selected operations.
/// Absence of this attribute keeps service-only access denied.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AllowGeneratedServiceAccessAttribute(
    params string[] allowedCallers) : Attribute
{
    public string[] AllowedCallers { get; } = allowedCallers;
    public string[] RequiredScopes { get; set; } = [];
    public EndpointActions Actions { get; set; } = EndpointActions.ReadOnly;
}
