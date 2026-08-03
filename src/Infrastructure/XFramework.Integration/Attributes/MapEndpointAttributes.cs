namespace XFramework.Integration.Attributes;

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
    /// Whether the generated endpoint should require authorization.
    /// Defaults to <c>false</c> to preserve existing method-level endpoint behavior.
    /// </summary>
    public bool RequireAuthorization { get; set; }

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
