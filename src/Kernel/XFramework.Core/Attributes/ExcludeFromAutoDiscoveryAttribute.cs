namespace XFramework.Core.Attributes;

/// <summary>
/// Marks an endpoint class or service interface/implementation to be excluded from automatic discovery and registration.
/// Use this attribute when you need manual control over the registration process.
/// </summary>
/// <remarks>
/// This attribute can be applied to:
/// <list type="bullet">
/// <item><description>Endpoint classes (e.g., ProductEndpoints) to prevent automatic mapping via MapGeneratedEndpoints()</description></item>
/// <item><description>Service interfaces (e.g., IProductService) to prevent automatic registration via AddGeneratedServices()</description></item>
/// <item><description>Service implementations (e.g., ProductService) to prevent automatic registration via AddGeneratedServices()</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Exclude an endpoint class from auto-discovery
/// [ExcludeFromAutoDiscovery]
/// public static class CustomProductEndpoints
/// {
///     public static IEndpointRouteBuilder MapCustomProductEndpoints(this IEndpointRouteBuilder app)
///     {
///         // Custom endpoint mapping logic
///         return app;
///     }
/// }
/// 
/// // Exclude a service from auto-discovery
/// [ExcludeFromAutoDiscovery]
/// public interface ICustomProductService
/// {
///     // Service methods
/// }
/// 
/// [ExcludeFromAutoDiscovery]
/// public class CustomProductService : ICustomProductService
/// {
///     // Service implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeFromAutoDiscoveryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an optional reason for excluding this type from auto-discovery.
    /// Useful for documentation and troubleshooting purposes.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludeFromAutoDiscoveryAttribute"/> class.
    /// </summary>
    public ExcludeFromAutoDiscoveryAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludeFromAutoDiscoveryAttribute"/> class with a reason.
    /// </summary>
    /// <param name="reason">The reason for excluding this type from auto-discovery</param>
    public ExcludeFromAutoDiscoveryAttribute(string reason)
    {
        Reason = reason;
    }
}