namespace XFramework.Domain.Shared.Attributes;

/// <summary>
/// Marks an entity class for automatic code generation of services and/or REST endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This attribute enables entity-centric code generation, reducing boilerplate code
/// by automatically creating service layer and/or minimal API endpoints for CRUD operations.
/// </para>
/// <para>
/// The attribute provides fine-grained control over:
/// <list type="bullet">
///   <item>What to generate (service only, endpoints only, or both)</item>
///   <item>Which CRUD operations to include</item>
///   <item>Routing configuration</item>
///   <item>Authorization requirements</item>
///   <item>Caching strategy</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Full CRUD with both service and endpoints
/// [GenerateEndpoints(
///     Type = EndpointType.Both,
///     Actions = EndpointActions.All,
///     RoutePrefix = "api/products"
/// )]
/// public partial class Product : BaseEntity
/// {
///     public string Name { get; set; } = string.Empty;
///     public decimal Price { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class GenerateEndpointsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets what type of code should be generated (service, endpoints, or both).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property determines the scope of code generation:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="EndpointType.Service"/>: Generate service layer only</item>
    ///   <item><see cref="EndpointType.Rest"/>: Generate REST endpoints only</item>
    ///   <item><see cref="EndpointType.Both"/>: Generate both service and endpoints (recommended)</item>
    /// </list>
    /// </remarks>
    /// <value>
    /// An <see cref="EndpointType"/> value. Defaults to <see cref="EndpointType.Both"/>.
    /// </value>
    /// <example>
    /// <code>
    /// // Generate both service and endpoints
    /// [GenerateEndpoints(Type = EndpointType.Both)]
    /// 
    /// // Generate service only (manual endpoints)
    /// [GenerateEndpoints(Type = EndpointType.Service)]
    /// </code>
    /// </example>
    public EndpointType Type { get; set; } = EndpointType.Both;

    /// <summary>
    /// Gets or sets which CRUD operations should be generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This flags enum allows fine-grained control over which operations are generated.
    /// Multiple flags can be combined using the bitwise OR operator (|).
    /// </para>
    /// <para>
    /// Common combinations:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="EndpointActions.All"/>: Full CRUD (Create, Get, GetList, Update, Delete)</item>
    ///   <item><see cref="EndpointActions.ReadOnly"/>: Get and GetList only</item>
    ///   <item><see cref="EndpointActions.Standard"/>: All except Delete</item>
    ///   <item><see cref="EndpointActions.WriteOnly"/>: Create, Update, Delete only</item>
    /// </list>
    /// </remarks>
    /// <value>
    /// An <see cref="EndpointActions"/> flags value. Defaults to <see cref="EndpointActions.All"/>.
    /// </value>
    /// <example>
    /// <code>
    /// // Generate all CRUD operations
    /// Actions = EndpointActions.All
    /// 
    /// // Generate only read operations
    /// Actions = EndpointActions.ReadOnly
    /// 
    /// // Generate Create and Update only
    /// Actions = EndpointActions.Create | EndpointActions.Update
    /// </code>
    /// </example>
    public EndpointActions Actions { get; set; } = EndpointActions.All;

    /// <summary>
    /// Gets or sets the base route prefix for generated endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property defines the base URL path for the generated endpoints.
    /// The route should follow REST conventions and typically start with "api/".
    /// </para>
    /// <para>
    /// Individual operation routes are appended to this prefix:
    /// </para>
    /// <list type="bullet">
    ///   <item>Create: POST {RoutePrefix}</item>
    ///   <item>Get: GET {RoutePrefix}/{id}</item>
    ///   <item>GetList: GET {RoutePrefix}</item>
    ///   <item>Update: PUT {RoutePrefix}/{id}</item>
    ///   <item>Delete: DELETE {RoutePrefix}/{id}</item>
    /// </list>
    /// </remarks>
    /// <value>
    /// A string representing the base route. Defaults to <c>null</c>, which uses the
    /// entity name in lowercase as the route (e.g., "api/products" for Product entity).
    /// </value>
    /// <example>
    /// <code>
    /// // Explicit route
    /// RoutePrefix = "api/products"
    /// 
    /// // Nested resource route
    /// RoutePrefix = "api/categories/{categoryId}/products"
    /// 
    /// // Versioned route
    /// RoutePrefix = "api/v2/products"
    /// </code>
    /// </example>
    public string? RoutePrefix { get; set; }

    /// <summary>
    /// Gets or sets whether generated endpoints require authorization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c>, the generated endpoints will require an authenticated user.
    /// Unauthenticated requests will receive a 401 Unauthorized response.
    /// </para>
    /// <para>
    /// This property applies the [Authorize] attribute to generated endpoints.
    /// For public endpoints (e.g., lookup data), set this to <c>false</c>.
    /// </para>
    /// <para>
    /// <strong>Security Note:</strong> This defaults to <c>true</c> for security by default.
    /// Carefully consider the security implications before setting to <c>false</c>.
    /// </para>
    /// </remarks>
    /// <value>
    /// <c>true</c> if authorization is required; otherwise, <c>false</c>. Defaults to <c>true</c>.
    /// </value>
    /// <example>
    /// <code>
    /// // Require authentication (default, secure)
    /// RequireAuthorization = true
    /// 
    /// // Allow anonymous access (public data)
    /// RequireAuthorization = false
    /// </code>
    /// </example>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>
    /// Gets or sets the roles required to access generated endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When specified, only users with at least one of the listed roles can access
    /// the generated endpoints. This property is only effective when
    /// <see cref="RequireAuthorization"/> is <c>true</c>.
    /// </para>
    /// <para>
    /// If <c>null</c> or empty, any authenticated user can access the endpoints
    /// (no role-based restriction).
    /// </para>
    /// </remarks>
    /// <value>
    /// An array of role names, or <c>null</c> if no role restriction is needed.
    /// Defaults to <c>null</c>.
    /// </value>
    /// <example>
    /// <code>
    /// // Require Admin or Manager role
    /// Roles = new[] { "Admin", "Manager" }
    /// 
    /// // Require Admin role only
    /// Roles = new[] { "Admin" }
    /// 
    /// // No role restriction (any authenticated user)
    /// Roles = null
    /// </code>
    /// </example>
    public string[]? Roles { get; set; }

    /// <summary>
    /// Canonical module or subfeature key used to compose actor capabilities for generated operations.
    /// </summary>
    /// <example><c>wallets.reporting</c></example>
    public string? AuthorizationFeature { get; set; }

    /// <summary>Capability key required for generated Get and GetList operations.</summary>
    public string ReadCapability { get; set; } = "view";

    /// <summary>Capability key required for generated Create operations.</summary>
    public string CreateCapability { get; set; } = "create";

    /// <summary>Capability key required for generated Update operations.</summary>
    public string UpdateCapability { get; set; } = "update";

    /// <summary>Capability key required for generated Delete operations.</summary>
    public string DeleteCapability { get; set; } = "delete";

    /// <summary>
    /// Actor requirement applied consistently to generated REST, service, Bolt, and remote data-context paths.
    /// </summary>
    public GeneratedActorRequirement ActorRequirement { get; set; } = GeneratedActorRequirement.Required;

    /// <summary>Tenant rule applied to every generated access path.</summary>
    public GeneratedTenantAccessMode TenantAccessMode { get; set; } = GeneratedTenantAccessMode.ActorTenant;

    /// <summary>Capability required when delegated access targets a tenant other than the actor tenant.</summary>
    public string CrossTenantCapability { get; set; } = "identity.tenants:manage";

    /// <summary>Explicit scalar properties exposed by generated response DTOs.</summary>
    public string[]? ResponseProperties { get; set; }

    /// <summary>
    /// Gets or sets the cache duration in seconds for GET operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property controls how long responses from GET and GetList operations
    /// are cached. Caching improves performance by reducing database queries for
    /// frequently accessed data.
    /// </para>
    /// <para>
    /// The cache is automatically invalidated when Create, Update, or Delete
    /// operations are performed on the entity.
    /// </para>
    /// <para>
    /// Set to 0 to disable caching for this entity.
    /// </para>
    /// </remarks>
    /// <value>
    /// The cache duration in seconds. Defaults to 300 (5 minutes).
    /// Must be a non-negative integer.
    /// </value>
    /// <example>
    /// <code>
    /// // Cache for 10 minutes
    /// CacheDurationSeconds = 600
    /// 
    /// // Cache for 1 hour (reference data)
    /// CacheDurationSeconds = 3600
    /// 
    /// // Disable caching (frequently changing data)
    /// CacheDurationSeconds = 0
    /// </code>
    /// </example>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the prefix used for cache keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property allows customization of cache key generation for this entity.
    /// Cache keys are formatted as: <c>{CacheKeyPrefix}:{EntityId}</c> for Get operations
    /// and <c>{CacheKeyPrefix}:list:{QueryHash}</c> for GetList operations.
    /// </para>
    /// <para>
    /// If <c>null</c> or empty, the entity name in lowercase is used as the prefix
    /// (e.g., "product" for Product entity).
    /// </para>
    /// </remarks>
    /// <value>
    /// A string representing the cache key prefix, or <c>null</c> to use the default.
    /// Defaults to <c>null</c>.
    /// </value>
    /// <example>
    /// <code>
    /// // Custom cache key prefix
    /// CacheKeyPrefix = "products"
    /// 
    /// // Module-scoped cache keys
    /// CacheKeyPrefix = "inventory:products"
    /// 
    /// // Use default (entity name)
    /// CacheKeyPrefix = null
    /// </code>
    /// </example>
    public string? CacheKeyPrefix { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateEndpointsAttribute"/> class.
    /// </summary>
    /// <remarks>
    /// All properties have secure defaults:
    /// <list type="bullet">
    ///   <item><see cref="Type"/>: <see cref="EndpointType.Both"/></item>
    ///   <item><see cref="Actions"/>: <see cref="EndpointActions.All"/></item>
    ///   <item><see cref="RequireAuthorization"/>: <c>true</c></item>
    ///   <item><see cref="CacheDurationSeconds"/>: 300 (5 minutes)</item>
    /// </list>
    /// </remarks>
    public GenerateEndpointsAttribute()
    {
    }
}
