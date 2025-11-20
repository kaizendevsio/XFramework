namespace XFramework.Core.Attributes;

/// <summary>
/// Specifies what type of code generation should be performed for an entity.
/// </summary>
/// <remarks>
/// This enum controls whether the source generator creates services, REST endpoints, or both
/// when processing entities marked with the <see cref="GenerateEndpointsAttribute"/>.
/// </remarks>
public enum EndpointType
{
    /// <summary>
    /// Generate service layer only, without REST endpoints.
    /// </summary>
    /// <remarks>
    /// Use this option when you need business logic services but want to manually create
    /// endpoints with custom behavior or routing. The generated service will include
    /// standard CRUD operations based on the specified <see cref="EndpointActions"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Type = EndpointType.Service)]
    /// public partial class ComplexEntity : BaseEntity { }
    /// </code>
    /// </example>
    Service = 1,
    
    /// <summary>
    /// Generate minimal API REST endpoints only, without service layer.
    /// </summary>
    /// <remarks>
    /// Use this option when business logic is simple or already exists elsewhere,
    /// and you only need standardized REST endpoints. The generated endpoints will
    /// directly interact with the repository layer.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Type = EndpointType.Rest)]
    /// public partial class LookupEntity : BaseEntity { }
    /// </code>
    /// </example>
    Rest = 2,
    
    /// <summary>
    /// Generate both service layer and REST endpoints.
    /// </summary>
    /// <remarks>
    /// This is the most common option, generating a complete CRUD implementation
    /// with service layer containing business logic and minimal API endpoints
    /// that call the service. This provides the standard VSA architecture pattern.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEndpoints(Type = EndpointType.Both)]
    /// public partial class Product : BaseEntity { }
    /// </code>
    /// </example>
    Both = 3
}