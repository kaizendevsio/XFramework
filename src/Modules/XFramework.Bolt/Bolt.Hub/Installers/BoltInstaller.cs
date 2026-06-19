using Bolt.Hub.Services;
using Bolt.Server;
using Microsoft.AspNetCore.ResponseCompression;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

/// <summary>
/// Installer for the Bolt thin-protocol server.
/// </summary>
public sealed class BoltInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Bind Bolt configuration
        var boltConfiguration = new BoltConfiguration();
        configuration.Bind(nameof(BoltConfiguration), boltConfiguration);
        services.AddSingleton(boltConfiguration);

        // Bolt thin protocol server
        services.AddBoltServer();
        services.AddSingleton<IBoltServicePresenceTracker, BoltServicePresenceTracker>();
        services.AddScoped<IBoltServiceDiscoveryRegistry, BoltServiceDiscoveryRegistry>();
        services.AddHostedService<BoltServiceDiscoveryHostedService>();

        // Durable queue store (Redis if configured, in-memory fallback)
        services.Configure<Bolt.Server.Durable.DurableQueueOptions>(configuration.GetSection("BoltConfiguration:Durable"));
        var redisConn = configuration["BoltConfiguration:Durable:RedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<Bolt.Server.Durable.IDurableQueueStore, Bolt.Server.Durable.RedisDurableQueueStore>();
        }
        else
        {
            services.AddSingleton<Bolt.Server.Durable.IDurableQueueStore, Bolt.Server.Durable.InMemoryDurableQueueStore>();
        }

        // Enable response compression for WebSocket connections
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/octet-stream" });
        });
    }
}
