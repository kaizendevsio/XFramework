using Bolt.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace XFramework.Integration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register BoltClient (thin protocol) and BoltDriver (IMessageBusWrapper) for service-to-service communication.
    /// Reads BoltConfiguration from appsettings.json (section "BoltConfiguration").
    /// Replaces the legacy SignalR-based driver registration.
    ///
    /// Usage:
    ///   builder.Services.AddXFrameworkBoltClient(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddXFrameworkBoltClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BoltConfiguration>(configuration.GetSection("BoltConfiguration"));

        var boltConfig = configuration.GetSection("BoltConfiguration").Get<BoltConfiguration>()
            ?? throw new InvalidOperationException("BoltConfiguration section is missing or empty in configuration.");

        if (boltConfig.ServerUrls is null || boltConfig.ServerUrls.Count == 0)
            throw new InvalidOperationException("BoltConfiguration:ServerUrls must contain at least one URL.");

        services.AddBoltClient(builder =>
        {
            builder
                .WithServer(boltConfig.ServerUrls[0])
                .WithClientId(boltConfig.ClientGuid?.ToString() ?? Guid.NewGuid().ToString())
                .WithClientName(boltConfig.ClientName ?? "unknown")
                .WithTimeout(boltConfig.RpcTimeoutSeconds);
        });

        services.AddSingleton<IMessageBusWrapper, BoltDriver>();

        return services;
    }
}
