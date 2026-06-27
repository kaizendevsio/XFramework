using Microsoft.Extensions.Logging;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for automatic discovery and registration of generated services
/// </summary>
public static class ServiceDiscoveryExtensions
{
    private static readonly object CacheLock = new();
    private static List<ServiceRegistration>? _cachedServiceRegistrations;
    private static bool _cacheInitialized;

    /// <summary>
    /// Automatically discovers and registers all generated service interfaces and implementations.
    /// This method scans for I*Service interfaces and their corresponding *Service implementations.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblyFilter">Optional filter to limit which assemblies are scanned.
    /// Defaults to assemblies starting with "XFramework" or module names</param>
    /// <param name="lifetime">Service lifetime to use. Defaults to Scoped for DbContext compatibility</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Auto-discover all services with Scoped lifetime
    /// builder.Services.AddGeneratedServices();
    /// 
    /// // Or with custom filter and lifetime
    /// builder.Services.AddGeneratedServices(
    ///     asm => asm.FullName?.Contains("MyProject") == true,
    ///     ServiceLifetime.Transient);
    /// </code>
    /// </example>
    public static IServiceCollection AddGeneratedServices(
        this IServiceCollection services,
        Func<Assembly, bool>? assemblyFilter = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        // Build a temporary service provider to get logger
        var tempProvider = services.BuildServiceProvider();
        var loggerFactory = tempProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("ServiceDiscovery");
        var startTime = DateTime.UtcNow;

        try
        {
            // Use cached registrations if available
            var serviceRegistrations = GetCachedServiceRegistrations(assemblyFilter);

            logger?.LogInformation(
                "Starting auto-discovery of services. Found {Count} service pairs to register",
                serviceRegistrations.Count);

            var registeredCount = 0;
            var skippedCount = 0;

            foreach (var registration in serviceRegistrations)
            {
                try
                {
                    // Check if service is already registered
                    if (services.Any(sd => sd.ServiceType == registration.InterfaceType))
                    {
                        logger?.LogDebug(
                            "Service {InterfaceType} already registered, skipping",
                            registration.InterfaceType.Name);
                        skippedCount++;
                        continue;
                    }

                    // Register the service
                    RegisterService(services, registration, lifetime);
                    registeredCount++;

                    logger?.LogDebug(
                        "Registered service {InterfaceType} -> {ImplementationType} with {Lifetime} lifetime",
                        registration.InterfaceType.Name,
                        registration.ImplementationType.Name,
                        lifetime);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex,
                        "Failed to register service {InterfaceType} -> {ImplementationType}",
                        registration.InterfaceType.Name,
                        registration.ImplementationType.Name);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            logger?.LogInformation(
                "Service auto-discovery completed in {Duration}ms. " +
                "Registered: {Registered}, Skipped: {Skipped}, Total: {Total}",
                duration.TotalMilliseconds,
                registeredCount,
                skippedCount,
                serviceRegistrations.Count);

            return services;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Critical error during service auto-discovery");
            return services; // Return services even on error to prevent app crash
        }
    }

    /// <summary>
    /// Gets or builds a cached list of service registrations matching the criteria
    /// </summary>
    private static List<ServiceRegistration> GetCachedServiceRegistrations(Func<Assembly, bool>? assemblyFilter)
    {
        lock (CacheLock)
        {
            if (_cacheInitialized && _cachedServiceRegistrations != null)
            {
                return _cachedServiceRegistrations;
            }

            _cachedServiceRegistrations = DiscoverServiceRegistrations(assemblyFilter);
            _cacheInitialized = true;
            return _cachedServiceRegistrations;
        }
    }

    /// <summary>
    /// Discovers all service interface/implementation pairs in the specified assemblies
    /// </summary>
    private static List<ServiceRegistration> DiscoverServiceRegistrations(Func<Assembly, bool>? assemblyFilter)
    {
        var registrations = new List<ServiceRegistration>();

        // Default filter: XFramework and module assemblies
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
                    var types = assembly.GetTypes();

                    // Find all interface types ending with "Service"
                    var serviceInterfaces = types
                        .Where(t => t.IsInterface &&
                                    t.Name.EndsWith("Service") &&
                                    !HasExcludeAttribute(t))
                        .ToList();

                    foreach (var interfaceType in serviceInterfaces)
                    {
                        // Find corresponding implementation
                        // Pattern: IProductService -> ProductService
                        var expectedImplementationName = interfaceType.Name.Substring(1); // Remove 'I' prefix
                        
                        var implementationType = types.FirstOrDefault(t =>
                            t.IsClass &&
                            !t.IsAbstract &&
                            t.Name == expectedImplementationName &&
                            interfaceType.IsAssignableFrom(t) &&
                            !HasExcludeAttribute(t));

                        if (implementationType != null)
                        {
                            registrations.Add(new ServiceRegistration(
                                interfaceType,
                                implementationType));
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle partially loaded assemblies
                    var loadedTypes = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                    
                    var serviceInterfaces = loadedTypes
                        .Where(t => t.IsInterface &&
                                    t.Name.EndsWith("Service") &&
                                    !HasExcludeAttribute(t))
                        .ToList();

                    foreach (var interfaceType in serviceInterfaces)
                    {
                        var expectedImplementationName = interfaceType.Name.Substring(1);
                        
                        var implementationType = loadedTypes.FirstOrDefault(t =>
                            t.IsClass &&
                            !t.IsAbstract &&
                            t.Name == expectedImplementationName &&
                            interfaceType.IsAssignableFrom(t) &&
                            !HasExcludeAttribute(t));

                        if (implementationType != null)
                        {
                            registrations.Add(new ServiceRegistration(
                                interfaceType,
                                implementationType));
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // If we can't scan assemblies, return empty list
            return new List<ServiceRegistration>();
        }

        return registrations;
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
    /// Registers a service with the specified lifetime
    /// </summary>
    private static void RegisterService(
        IServiceCollection services,
        ServiceRegistration registration,
        ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(registration.InterfaceType, registration.ImplementationType);
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(registration.InterfaceType, registration.ImplementationType);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(registration.InterfaceType, registration.ImplementationType);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    /// <summary>
    /// Manually registers a specific service interface/implementation pair.
    /// Useful for selective registration or overriding auto-discovered services.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">Service lifetime to use. Defaults to Scoped</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Manually register a specific service
    /// builder.Services.AddService&lt;IProductService, ProductService&gt;();
    /// 
    /// // Or with custom lifetime
    /// builder.Services.AddService&lt;IProductService, ProductService&gt;(ServiceLifetime.Singleton);
    /// </code>
    /// </example>
    public static IServiceCollection AddService<TInterface, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<TInterface, TImplementation>();
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<TInterface, TImplementation>();
                break;
            case ServiceLifetime.Transient:
                services.AddTransient<TInterface, TImplementation>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }

        return services;
    }

    /// <summary>
    /// Clears the cached service registrations. Useful for testing or dynamic assembly loading scenarios.
    /// </summary>
    public static void ClearServiceCache()
    {
        lock (CacheLock)
        {
            _cachedServiceRegistrations = null;
            _cacheInitialized = false;
        }
    }

    /// <summary>
    /// Represents a discovered service registration pair
    /// </summary>
    private record ServiceRegistration(Type InterfaceType, Type ImplementationType);
}