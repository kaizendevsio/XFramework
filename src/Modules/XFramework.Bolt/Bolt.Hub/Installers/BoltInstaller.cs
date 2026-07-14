using Bolt.Hub.Health;
using Bolt.Hub.Services;
using Bolt.Hub.Security;
using Bolt.Server;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Domain.Shared.ServiceIdentity;

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
        services.AddBoltServer(options =>
        {
            options.InvocationTimeoutMs = Math.Max(1, boltConfiguration.RpcTimeoutSeconds) * 1000;
            options.MaxFrameBytes = boltConfiguration.MaxFrameBytes > 0
                ? boltConfiguration.MaxFrameBytes
                : options.MaxFrameBytes;
            options.SendQueueCapacity = boltConfiguration.SendQueueCapacity > 0
                ? boltConfiguration.SendQueueCapacity
                : boltConfiguration.QueueDepth > 0
                    ? boltConfiguration.QueueDepth
                    : options.SendQueueCapacity;
            options.SendEnqueueTimeoutMs = boltConfiguration.SendEnqueueTimeoutMs;
            options.RequireSecureTransport = !hostEnvironment.IsDevelopment() ||
                                             boltConfiguration.RequireSecureTransport;
            options.MediaEnabled = boltConfiguration.MediaEnabled;
            options.MaxPendingRpcCalls = boltConfiguration.MaxPendingRpcCalls;
            options.MaxPendingRpcCallsPerPrincipal = boltConfiguration.MaxPendingRpcCallsPerPrincipal;
            options.MaxConnectionsPerPrincipal = boltConfiguration.MaxConnectionsPerPrincipal;
            options.MaxActiveStreamsPerPrincipal = boltConfiguration.MaxActiveStreamsPerPrincipal;
            options.MaxMediaStreamsPerPrincipal = boltConfiguration.MaxMediaStreamsPerPrincipal;
            options.MaxSubscriptionsPerPrincipal = boltConfiguration.MaxSubscriptionsPerPrincipal;
            options.MaxDurableSubscribersPerTopic = boltConfiguration.MaxDurableSubscribersPerTopic;
            options.MaxConnectionLifetimeSeconds = boltConfiguration.MaxConnectionLifetimeSeconds;
            options.RegistrationIdentityBindingMode = ResolveRegistrationIdentityBindingMode(
                boltConfiguration.RegistrationIdentityBindingMode,
                hostEnvironment);
            options.ReservedServiceNames.AddRange(XFrameworkServiceNames.All);
            options.ReservedServiceNamePrefixes.Add("XFramework.");
            configuration
                .GetSection("BoltConfiguration:RegistrationMigrationAllowances")
                .Bind(options.RegistrationMigrationAllowances);
        });
        services.AddSingleton<IBoltTopicAuthorizer, CommunicationsBoltTopicAuthorizer>();
        services.AddSingleton<IBoltServicePresenceTracker, BoltServicePresenceTracker>();
        services.AddScoped<IBoltServiceDiscoveryRegistry, BoltServiceDiscoveryRegistry>();
        services.AddHostedService<BoltServiceDiscoveryHostedService>();
        services.AddAuthorization(options =>
        {
            BoltAuthorizationPolicies.AddTransportPolicy(options);
            BoltAuthorizationPolicies.AddServiceDiscoveryReaderPolicy(options);
        });
        services.AddHealthChecks().AddCheck<BoltTransportHealthCheck>(
            "Bolt-transport",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["bolt", "transport", "ready"]);
        services.AddHealthChecks().AddCheck<BoltTransportIdentityHealthCheck>(
            "Bolt-transport-identity",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["bolt", "identity", "ready"]);

        // Durable queue store (Redis required outside Development)
        services.Configure<Bolt.Server.Durable.DurableQueueOptions>(configuration.GetSection("BoltConfiguration:Durable"));
        var redisConn = configuration["BoltConfiguration:Durable:RedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<Bolt.Server.Durable.IDurableQueueStore, Bolt.Server.Durable.RedisDurableQueueStore>();
            services.AddHealthChecks().AddRedis(
                redisConn,
                name: "Bolt-durable-redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["durable", "redis", "ready"]);
        }
        else
        {
            if (!hostEnvironment.IsDevelopment())
                throw new InvalidOperationException("BoltConfiguration:Durable:RedisConnectionString is required outside Development for durable Bolt subscriptions.");

            services.AddSingleton<Bolt.Server.Durable.IDurableQueueStore, Bolt.Server.Durable.InMemoryDurableQueueStore>();
        }

        // Enable response compression for WebSocket connections
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/octet-stream" });
        });
    }

    private static BoltRegistrationIdentityBindingMode ResolveRegistrationIdentityBindingMode(
        string? value,
        IHostEnvironment hostEnvironment)
    {
        var mode = BoltRegistrationIdentityBindingMode.Enforce;
        if (!string.IsNullOrWhiteSpace(value) &&
            (!Enum.TryParse(value, ignoreCase: true, out mode) || !Enum.IsDefined(mode)))
        {
            throw new InvalidOperationException(
                "BoltConfiguration:RegistrationIdentityBindingMode must be one of: Off, Audit, Enforce.");
        }

        if (!hostEnvironment.IsDevelopment() && mode != BoltRegistrationIdentityBindingMode.Enforce)
        {
            throw new InvalidOperationException(
                $"BoltConfiguration:RegistrationIdentityBindingMode '{mode}' is allowed only in Development. " +
                $"Environment '{hostEnvironment.EnvironmentName}' requires Enforce.");
        }

        return mode;
    }
}
