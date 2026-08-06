namespace XFramework.Integration.Attributes;

using XFramework.Integration.Security;

/// <summary>
/// Base class for REST endpoint mapping attributes.
/// The source generator uses these to generate the endpoint registration code
/// and the REST adapter that converts Result&lt;T&gt; to HTTP responses.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public abstract class MapEndpointAttribute : Attribute
{
    /// <summary>The route pattern (e.g. "/api/auth/authenticate")</summary>
    public string Route { get; }

    /// <summary>OpenAPI tags for grouping endpoints</summary>
    public string[]? Tags { get; set; }

    /// <summary>OpenAPI summary</summary>
    public string? Summary { get; set; }

    /// <summary>OpenAPI description</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether to exclude this endpoint from the OpenAPI document.
    /// Workaround for dotnet/aspnetcore#63857 (circular navigation properties).
    /// </summary>
    public bool ExcludeFromOpenApi { get; set; }

    /// <summary>
    /// Whether the generated endpoint should establish a trusted invocation and require ASP.NET authorization.
    /// Defaults to <c>true</c>. Public endpoints must declare an explicit anonymous invocation policy.
    /// </summary>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>Service-token scopes required by this REST endpoint.</summary>
    public string[]? RequiredServiceScopes { get; set; }

    /// <summary>Service client IDs allowed to invoke this REST endpoint.</summary>
    public string[]? AllowedServiceCallers { get; set; }

    /// <summary>Whether the invocation must carry a validated actor identity.</summary>
    public ActorRequirement ActorRequirement { get; set; } = ActorRequirement.Required;

    /// <summary>How the effective tenant is derived for this REST endpoint.</summary>
    public TenantAccessMode TenantAccessMode { get; set; } = TenantAccessMode.ActorTenant;

    /// <summary>Actor capabilities required by this REST endpoint.</summary>
    public string[]? RequiredActorCapabilities { get; set; }

    /// <summary>Actor capabilities required only when targeting a tenant other than the actor tenant.</summary>
    public string[]? RequiredCrossTenantActorCapabilities { get; set; }

    /// <summary>Whether this endpoint intentionally accepts requests without actor or service identity.</summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Named authorization policy required by the generated endpoint.
    /// When set, the generator emits RequireAuthorization for this policy.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Roles required by the generated endpoint.
    /// When set, the generator emits RequireAuthorization with a role requirement.
    /// </summary>
    public string[]? Roles { get; set; }

    /// <summary>Named ASP.NET Core rate-limit policy required by the endpoint.</summary>
    public string? RateLimitPolicy { get; set; }

    /// <summary>Tenant capability required by the endpoint.</summary>
    public string? Capability { get; set; }

    protected MapEndpointAttribute(string route) => Route = route;
}

/// <summary>Maps the handler as an HTTP POST endpoint.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapPostAttribute(string route) : MapEndpointAttribute(route);

/// <summary>Maps the handler as an HTTP GET endpoint.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapGetAttribute(string route) : MapEndpointAttribute(route);

/// <summary>Maps the handler as an HTTP PUT endpoint.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapPutAttribute(string route) : MapEndpointAttribute(route);

/// <summary>Maps the handler as an HTTP PATCH endpoint.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapPatchAttribute(string route) : MapEndpointAttribute(route);

/// <summary>Maps the handler as an HTTP DELETE endpoint.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapDeleteAttribute(string route) : MapEndpointAttribute(route);
