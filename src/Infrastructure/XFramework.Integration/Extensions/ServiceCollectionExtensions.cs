using System.Reflection;
using Bolt.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Abstractions;
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

        // CRITICAL: Register handler scan BEFORE AddBoltClient so it runs before auto-connect.
        // Hosted services start in registration order; handlers must be registered in BoltClient
        // before the connection is established so no incoming frames are dropped.
        services.AddHostedService<BoltHandlerRegistrationHostedService>();

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

/// <summary>
/// Hosted service that scans the entry assembly for source-generated IBoltHandler types
/// and registers them on the BoltClient at startup, before the auto-connect hosted service runs.
/// </summary>
internal sealed class BoltHandlerRegistrationHostedService : IHostedService
{
    private readonly BoltClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BoltHandlerRegistrationHostedService> _logger;

    public BoltHandlerRegistrationHostedService(
        BoltClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<BoltHandlerRegistrationHostedService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is null)
        {
            _logger.LogWarning("No entry assembly — skipping IBoltHandler scan");
            return Task.CompletedTask;
        }

        // Scan for all IBoltHandler implementations in the entry assembly
        var handlerTypes = entryAssembly.GetTypes()
            .Where(t => !t.IsInterface && !t.IsAbstract && typeof(IBoltHandler).IsAssignableFrom(t))
            .ToList();

        var registered = 0;
        foreach (var handlerType in handlerTypes)
        {
            try
            {
                var handler = (IBoltHandler)Activator.CreateInstance(handlerType)!;
                handler.Register(_client, _logger, _scopeFactory);
                registered++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register Bolt handler {Type}", handlerType.FullName);
            }
        }

        _logger.LogInformation("Registered {Count} Bolt handler(s) from entry assembly '{Assembly}'",
            registered, entryAssembly.GetName().Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
