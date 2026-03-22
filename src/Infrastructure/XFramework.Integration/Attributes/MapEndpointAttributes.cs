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
