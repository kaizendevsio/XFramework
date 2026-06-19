using System.Runtime.CompilerServices;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Core.Services.FeatureGates;

namespace XFramework.Core.Extensions;

public static class TenantModuleFeatureExtensions
{
    public static IServiceCollection AddTenantModuleFeatures(this IServiceCollection services)
    {
        RuntimeHelpers.RunClassConstructor(typeof(TenantModuleFeature).TypeHandle);
        services.AddTenantModuleFeatureDefinitions();
        services.AddMemoryCache();
        services.TryAddScoped<ITenantModuleFeatureService, TenantModuleFeatureService>();

        return services;
    }

    public static IServiceCollection AddTenantModuleFeatures(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        RuntimeHelpers.RunClassConstructor(typeof(TenantModuleFeature).TypeHandle);
        services.AddTenantModuleFeatureDefinitions(configuration);
        services.AddMemoryCache();
        services.TryAddScoped<ITenantModuleFeatureService, TenantModuleFeatureService>();

        return services;
    }

    public static IApplicationBuilder UseTenantModuleFeatureGate(
        this IApplicationBuilder app,
        Action<TenantModuleFeatureGateOptions> configure)
    {
        var options = new TenantModuleFeatureGateOptions();
        configure(options);

        return app.UseMiddleware<TenantModuleFeatureGateMiddleware>(options);
    }
}
