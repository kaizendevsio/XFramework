using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for automatic discovery and registration of generated endpoints
/// </summary>
public static class EndpointDiscoveryExtensions
{
    private static readonly object CacheLock = new();
    private static List<Type>? _cachedEndpointTypes;
    private static bool _cacheInitialized;

    /// <summary>
    /// Automatically discovers and maps all generated endpoint classes in loaded assemblies.
    /// This method scans for types ending with "Endpoints" and invokes their Map*Endpoints() methods.
    /// </summary>
    /// <param name="app">The endpoint route builder</param>
    /// <param name="assemblyFilter">Optional filter to limit which assemblies are scanned. 
    /// Defaults to assemblies starting with "XFramework" or "Inventario"</param>
    /// <returns>The endpoint route builder for chaining</returns>
    /// <example>
    /// <code>
    /// // Auto-discover all endpoints in XFramework and Inventario assemblies
    /// app.MapGeneratedEndpoints();
    /// 
    /// // Or with custom filter
    /// app.MapGeneratedEndpoints(asm => asm.FullName?.Contains("MyProject") == true);
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapGeneratedEndpoints(
        this IEndpointRouteBuilder app,
        Func<Assembly, bool>? assemblyFilter = null)
    {
        var loggerFactory = app.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("EndpointDiscovery");
        var startTime = DateTime.UtcNow;

        try
        {
            // Use cached types if available
            var endpointTypes = GetCachedEndpointTypes(assemblyFilter);

            logger?.LogInformation(
                "Starting auto-discovery of endpoints. Found {Count} endpoint types to register",
                endpointTypes.Count);

            var registeredCount = 0;
            var failedCount = 0;

            foreach (var endpointType in endpointTypes)
            {
                try
                {
                    if (RegisterEndpoint(app, endpointType, logger))
                    {
                        registeredCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    logger?.LogError(ex,
                        "Failed to register endpoint type {TypeName}",
                        endpointType.FullName);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            logger?.LogInformation(
                "Endpoint auto-discovery completed in {Duration}ms. " +
                "Registered: {Registered}, Failed: {Failed}, Total: {Total}",
                duration.TotalMilliseconds,
                registeredCount,
                failedCount,
                endpointTypes.Count);

            return app;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Critical error during endpoint auto-discovery");
            throw;
        }
    }

    /// <summary>
    /// Gets or builds a cached list of endpoint types matching the criteria
    /// </summary>
    private static List<Type> GetCachedEndpointTypes(Func<Assembly, bool>? assemblyFilter)
    {
        lock (CacheLock)
        {
            if (_cacheInitialized && _cachedEndpointTypes != null)
            {
                return _cachedEndpointTypes;
            }

            _cachedEndpointTypes = DiscoverEndpointTypes(assemblyFilter);
            _cacheInitialized = true;
            return _cachedEndpointTypes;
        }
    }

    /// <summary>
    /// Discovers all endpoint types in the specified assemblies
    /// </summary>
    private static List<Type> DiscoverEndpointTypes(Func<Assembly, bool>? assemblyFilter)
    {
        var endpointTypes = new List<Type>();
        
        // Default filter: XFramework and Inventario assemblies
        assemblyFilter ??= DefaultAssemblyFilter;

        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && assemblyFilter(a))
                .ToList();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass &&
                                    !t.IsAbstract &&
                                    t.Name.EndsWith("Endpoints") &&
                                    !HasExcludeAttribute(t))
                        .ToList();

                    endpointTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle partially loaded assemblies
                    var loadedTypes = ex.Types.Where(t => t != null).Cast<Type>();
                    var validTypes = loadedTypes
                        .Where(t => t.IsClass &&
                                    !t.IsAbstract &&
                                    t.Name.EndsWith("Endpoints") &&
                                    !HasExcludeAttribute(t))
                        .ToList();
                    
                    endpointTypes.AddRange(validTypes);
                }
            }
        }
        catch (Exception)
        {
            // If we can't scan assemblies, return empty list
            // Logger not available at this level
            return new List<Type>();
        }

        return endpointTypes;
    }

    /// <summary>
    /// Default assembly filter for XFramework projects
    /// </summary>
    private static bool DefaultAssemblyFilter(Assembly assembly)
    {
        var name = assembly.FullName ?? string.Empty;
        return name.StartsWith("XFramework", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Inventario", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Wallets", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Communications", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type has the ExcludeFromAutoDiscovery attribute
    /// </summary>
    private static bool HasExcludeAttribute(Type type)
    {
        return type.GetCustomAttributes(inherit: true)
            .Any(attr => attr.GetType().Name == "ExcludeFromAutoDiscoveryAttribute");
    }

    /// <summary>
    /// Registers a single endpoint type by finding and invoking its Map method
    /// </summary>
    private static bool RegisterEndpoint(
        IEndpointRouteBuilder app,
        Type endpointType,
        Microsoft.Extensions.Logging.ILogger? logger)
    {
        var mapMethod = GetEndpointMapMethod(endpointType);

        if (mapMethod == null)
        {
            logger?.LogWarning(
                "No suitable Map method found for endpoint type {TypeName}. " +
                "Expected pattern: Map{EndpointTypeName}(IEndpointRouteBuilder)",
                endpointType.Name,
                endpointType.Name);
            return false;
        }

        try
        {
            // Invoke the static Map method
            mapMethod.Invoke(null, new object[] { app });
            
            logger?.LogDebug(
                "Successfully registered endpoints from {TypeName} via {MethodName}",
                endpointType.Name,
                mapMethod.Name);
            
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "Failed to invoke {MethodName} on {TypeName}",
                mapMethod.Name,
                endpointType.Name);
            return false;
        }
    }

    /// <summary>
    /// Finds the appropriate Map*Endpoints method for the given type
    /// </summary>
    private static MethodInfo? GetEndpointMapMethod(Type endpointType)
    {
        // Look for a method matching the pattern: Map{TypeName}(IEndpointRouteBuilder)
        // Example: ProductEndpoints -> MapProductEndpoints
        var expectedMethodName = $"Map{endpointType.Name}";

        var method = endpointType.GetMethod(
            expectedMethodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(IEndpointRouteBuilder) },
            null);

        if (method != null && method.ReturnType == typeof(IEndpointRouteBuilder))
        {
            return method;
        }

        // Fallback: Look for any public static method that takes IEndpointRouteBuilder
        // and returns IEndpointRouteBuilder, and name contains "Map"
        var methods = endpointType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("Map") &&
                        m.ReturnType == typeof(IEndpointRouteBuilder) &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(IEndpointRouteBuilder))
            .ToList();

        return methods.FirstOrDefault();
    }

    /// <summary>
    /// Manually registers a specific endpoint type. Useful for selective registration.
    /// </summary>
    /// <typeparam name="TEndpoint">The endpoint type to register</typeparam>
    /// <param name="app">The endpoint route builder</param>
    /// <returns>The endpoint route builder for chaining</returns>
    /// <example>
    /// <code>
    /// // Manually register a specific endpoint type
    /// app.MapEndpoint&lt;ProductEndpoints&gt;();
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : class
    {
        var loggerFactory = app.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("EndpointDiscovery");
        var endpointType = typeof(TEndpoint);

        if (RegisterEndpoint(app, endpointType, logger))
        {
            logger?.LogInformation(
                "Manually registered endpoint type {TypeName}",
                endpointType.Name);
        }
        else
        {
            logger?.LogWarning(
                "Failed to manually register endpoint type {TypeName}",
                endpointType.Name);
        }

        return app;
    }

    /// <summary>
    /// Clears the cached endpoint types. Useful for testing or dynamic assembly loading scenarios.
    /// </summary>
    public static void ClearEndpointCache()
    {
        lock (CacheLock)
        {
            _cachedEndpointTypes = null;
            _cacheInitialized = false;
        }
    }
}