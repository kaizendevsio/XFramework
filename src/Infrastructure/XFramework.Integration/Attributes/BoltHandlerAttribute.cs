namespace XFramework.Integration.Attributes;

using XFramework.Integration.Security;

/// <summary>
/// Marks a static method as a Bolt handler in addition to being a REST endpoint handler.
/// The source generator scans for this attribute and generates the IBoltHandler adapter
/// that routes incoming Bolt thin-protocol messages to this method.
///
/// The decorated method must:
///   - Be static
///   - Have its first parameter be the request type (implementing IBoltRequest)
///   - Return Task&lt;Result&lt;T&gt;&gt; or Task&lt;Result&gt;
///   - Have remaining parameters resolvable from DI (services, CancellationToken)
///
/// Example:
///   [BoltHandler]
///   public static async Task&lt;Result&lt;AuthenticateIdentityResponse&gt;&gt; Handle(
///       AuthenticateIdentityRequest request,
///       IAuthService authService,
///       CancellationToken ct) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BoltHandlerAttribute : Attribute
{
    /// <summary>Service-token scopes required in addition to baseline token validation.</summary>
    public string[]? RequiredServiceScopes { get; set; }

    /// <summary>Service client IDs allowed to invoke this handler.</summary>
    public string[]? AllowedServiceCallers { get; set; }

    /// <summary>Whether the invocation must carry a validated actor identity.</summary>
    public ActorRequirement ActorRequirement { get; set; } = ActorRequirement.Required;

    /// <summary>How the effective tenant is derived for this operation.</summary>
    public TenantAccessMode TenantAccessMode { get; set; } = TenantAccessMode.ActorTenant;

    /// <summary>Actor capabilities required when delegated tenant access is requested.</summary>
    public string[]? RequiredActorCapabilities { get; set; }

    /// <summary>Actor capabilities required only when targeting a tenant other than the actor tenant.</summary>
    public string[]? RequiredCrossTenantActorCapabilities { get; set; }

    /// <summary>Whether this operation intentionally accepts requests without an actor or service identity.</summary>
    public bool AllowAnonymous { get; set; }
}
