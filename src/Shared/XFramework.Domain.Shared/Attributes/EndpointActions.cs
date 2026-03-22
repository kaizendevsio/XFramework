namespace XFramework.Domain.Shared.Attributes;

/// <summary>
/// Specifies which CRUD operations should be generated for an entity.
/// </summary>
/// <remarks>
/// This flags enum allows fine-grained control over which operations are generated.
/// Multiple flags can be combined using the bitwise OR operator (|).
/// </remarks>
/// <example>
/// <code>
/// // Generate only Create and Get operations
/// Actions = EndpointActions.Create | EndpointActions.Get
/// 
/// // Use predefined combination for read-only access
/// Actions = EndpointActions.ReadOnly
/// </code>
/// </example>
[Flags]
public enum EndpointActions
{
    /// <summary>
    /// No operations will be generated.
    /// </summary>
    /// <remarks>
    /// This is typically not used directly, but serves as the zero value for the enum.
    /// </remarks>
    None = 0,
    
    /// <summary>
    /// Generate Create operation (HTTP POST).
    /// </summary>
    /// <remarks>
    /// Generates an endpoint that accepts POST requests to create new entity instances.
    /// The endpoint will validate the input, create the entity, and return the created resource.
    /// </remarks>
    /// <example>
    /// POST /api/products
    /// {
    ///   "name": "New Product",
    ///   "price": 29.99
    /// }
    /// </example>
    Create = 1 << 0,
    
    /// <summary>
    /// Generate Get by ID operation (HTTP GET /{id}).
    /// </summary>
    /// <remarks>
    /// Generates an endpoint that accepts GET requests with an ID parameter to retrieve
    /// a single entity instance. Returns 404 if the entity is not found.
    /// </remarks>
    /// <example>
    /// GET /api/products/123
    /// </example>
    Get = 1 << 1,
    
    /// <summary>
    /// Generate Get List operation (HTTP GET /).
    /// </summary>
    /// <remarks>
    /// Generates an endpoint that accepts GET requests to retrieve a paginated list of entities.
    /// Supports query parameters for filtering, sorting, and pagination.
    /// </remarks>
    /// <example>
    /// GET /api/products?page=1&amp;pageSize=20&amp;sortBy=name
    /// </example>
    GetList = 1 << 2,
    
    /// <summary>
    /// Generate Update operation (HTTP PUT /{id}).
    /// </summary>
    /// <remarks>
    /// Generates an endpoint that accepts PUT requests with an ID parameter to update
    /// an existing entity. The entire entity must be provided in the request body.
    /// Returns 404 if the entity is not found.
    /// </remarks>
    /// <example>
    /// PUT /api/products/123
    /// {
    ///   "name": "Updated Product",
    ///   "price": 39.99
    /// }
    /// </example>
    Update = 1 << 3,
    
    /// <summary>
    /// Generate Delete operation (HTTP DELETE /{id}).
    /// </summary>
    /// <remarks>
    /// Generates an endpoint that accepts DELETE requests with an ID parameter to delete
    /// an entity. Returns 404 if the entity is not found, 204 No Content on success.
    /// </remarks>
    /// <example>
    /// DELETE /api/products/123
    /// </example>
    Delete = 1 << 4,
    
    /// <summary>
    /// Generate all CRUD operations (Create, Get, GetList, Update, Delete).
    /// </summary>
    /// <remarks>
    /// This is a convenience combination that includes all standard CRUD operations.
    /// Use this for entities that require full CRUD functionality.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Actions = EndpointActions.All)]
    /// public partial class Product : BaseEntity { }
    /// </code>
    /// </example>
    All = Create | Get | GetList | Update | Delete,
    
    /// <summary>
    /// Generate only read operations (Get, GetList).
    /// </summary>
    /// <remarks>
    /// This is a convenience combination for read-only entities such as lookup tables,
    /// reference data, or entities that should not be modified through the API.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Actions = EndpointActions.ReadOnly)]
    /// public partial class Category : BaseEntity { }
    /// </code>
    /// </example>
    ReadOnly = Get | GetList,
    
    /// <summary>
    /// Generate only write operations (Create, Update, Delete).
    /// </summary>
    /// <remarks>
    /// This is a convenience combination for scenarios where read operations are handled
    /// separately or through different endpoints, but write operations follow standard patterns.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Actions = EndpointActions.WriteOnly)]
    /// public partial class AuditLog : BaseEntity { }
    /// </code>
    /// </example>
    WriteOnly = Create | Update | Delete,
    
    /// <summary>
    /// Generate standard CRUD operations excluding Delete (Create, Get, GetList, Update).
    /// </summary>
    /// <remarks>
    /// This is a convenience combination for entities where soft deletes are preferred
    /// or where deletion should be handled through a different mechanism (e.g., archival).
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Actions = EndpointActions.Standard)]
    /// public partial class Customer : BaseEntity { }
    /// </code>
    /// </example>
    Standard = Create | Get | GetList | Update
}