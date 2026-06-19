using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityServer.Domain.Shared.Contracts;

public static class TenantModuleFeatureDefinitionServiceCollectionExtensions
{
    public static IServiceCollection AddTenantModuleFeatureDefinitions(this IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITenantModuleFeatureDefinitionProvider, BuiltInTenantModuleFeatureDefinitionProvider>());
        services.TryAddSingleton<ITenantModuleFeatureCatalog, TenantModuleFeatureCatalog>();

        return services;
    }

    public static IServiceCollection AddTenantModuleFeatureDefinitions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTenantModuleFeatureDefinitions();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantModuleFeatureDefinitionProvider>(
            new ConfigurationTenantModuleFeatureDefinitionProvider(configuration)));

        return services;
    }

    public static IServiceCollection AddTenantModuleFeatureDefinitions(
        this IServiceCollection services,
        ITenantModuleFeatureDefinitionProvider provider)
    {
        services.AddTenantModuleFeatureDefinitions();
        services.AddSingleton(provider);

        return services;
    }

    public static IServiceCollection AddTenantModuleFeatureDefinitionProvider<TProvider>(
        this IServiceCollection services)
        where TProvider : class, ITenantModuleFeatureDefinitionProvider
    {
        services.AddTenantModuleFeatureDefinitions();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITenantModuleFeatureDefinitionProvider, TProvider>());

        return services;
    }
}
