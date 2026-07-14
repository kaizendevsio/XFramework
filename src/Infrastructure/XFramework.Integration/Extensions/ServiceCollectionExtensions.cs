using System.Reflection;
using Bolt.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.DataContext;
using XFramework.Integration.Drivers;
using XFramework.Integration.Health;
using XFramework.Integration.Security;
using XFramework.Integration.ServiceDiscovery;

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
    public static IServiceCollection AddXFrameworkBoltClient(
        this IServiceCollection services,
        IConfiguration configuration,
        bool autoConnect = true,
        IHostEnvironment? hostEnvironment = null,
        bool connectAfterApplicationStarted = false)
    {
        services.Configure<BoltConfiguration>(configuration.GetSection("BoltConfiguration"));
        var boltConfig = configuration.GetSection("BoltConfiguration").Get<BoltConfiguration>()
            ?? throw new InvalidOperationException("BoltConfiguration section is missing or empty in configuration.");

        if (boltConfig.ServerUrls is null || boltConfig.ServerUrls.Count == 0)
            throw new InvalidOperationException("BoltConfiguration:ServerUrls must contain at least one URL.");

        ValidateTransportIdentityModes(boltConfig, hostEnvironment);

        services.TryAddSingleton(TimeProvider.System);
        services.AddCredentialGenerationHealthCheck();
        if (!connectAfterApplicationStarted)
            services.AddBoltClientTransportHealthCheck();
        var serviceIdentityOptions = services.AddOptions<ServiceIdentityOptions>()
            .Configure(options =>
            {
                configuration.GetSection(ServiceIdentityOptions.SectionName).Bind(options);
                options.ClientId = string.IsNullOrWhiteSpace(options.ClientId)
                    ? configuration["BoltConfiguration:ClientName"]
                    : options.ClientId;

                if (options.DefaultScopes.Count == 0)
                    options.DefaultScopes = XFrameworkServiceScopes.AdminDefaults.ToList();
            });
        if (UsesCentralTransportIdentity(boltConfig))
            serviceIdentityOptions.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ServiceIdentityOptions>, ServiceIdentityOptionsValidator>());
        services.AddHttpClient(ServiceIdentityHttpClient.Name, (serviceProvider, client) =>
        {
            client.BaseAddress = serviceProvider
                .GetRequiredService<IOptions<ServiceIdentityOptions>>()
                .Value
                .ResolveAuthority();
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<IBoltTransportTokenProvider, IdentityServerBoltTransportTokenProvider>();
        services.TryAddSingleton<IServiceTokenProvider, IdentityServerServiceTokenProvider>();
        services.Configure<BoltServiceDiscoveryOptions>(
            configuration.GetSection(BoltServiceDiscoveryOptions.SectionName));

        var requireSecureTransport = boltConfig.RequireSecureTransport;
        BoltClientSecureTransportValidator.Validate(boltConfig, requireSecureTransport);
        if (requireSecureTransport)
        {
            services.PostConfigure<BoltConfiguration>(options =>
                options.RequireSecureTransport = true);
        }

        // CRITICAL: Register handler scan BEFORE AddBoltClient so it runs before auto-connect.
        // Hosted services start in registration order; handlers must be registered in BoltClient
        // before the connection is established so no incoming frames are dropped.
        services.AddHostedService<BoltHandlerRegistrationHostedService>();

        // Client ID MUST be SHA256(ClientName) — the source-generated service wrappers
        // use SHA256(moduleName) as TargetClient for routing. If the service registers
        // with a different ID, the hub can't route requests to it (404).
        var clientName = boltConfig.ClientName ?? "unknown";
        var clientId = Security.Cryptography.ToSha256(clientName);

        services.AddBoltClient(builder =>
        {
            builder
                .WithServer(boltConfig.ServerUrls[0])
                .WithClientId(clientId)
                .WithClientName(clientName)
                .WithTimeout(boltConfig.RpcTimeoutSeconds)
                .UseAccessTokenQueryString(boltConfig.SendAccessTokenAsQueryString)
                .WithOptions(options =>
                {
                    options.MaxFrameBytes = boltConfig.MaxFrameBytes > 0
                        ? boltConfig.MaxFrameBytes
                        : options.MaxFrameBytes;
                    options.SendQueueCapacity = boltConfig.SendQueueCapacity > 0
                        ? boltConfig.SendQueueCapacity
                        : boltConfig.QueueDepth > 0
                            ? boltConfig.QueueDepth
                            : options.SendQueueCapacity;
                    options.SendEnqueueTimeoutMs = boltConfig.SendEnqueueTimeoutMs > 0
                        ? boltConfig.SendEnqueueTimeoutMs
                        : Math.Max(1, boltConfig.RpcTimeoutSeconds) * 1000;
                    options.MinConnections = Math.Max(1, boltConfig.MinConnections);
                    options.MaxConnections = Math.Max(
                        options.MinConnections,
                        boltConfig.MaxConnections > 0 ? boltConfig.MaxConnections : options.MaxConnections);
                    options.ScaleUpThreshold = boltConfig.ScaleUpThreshold > 0
                        ? boltConfig.ScaleUpThreshold
                        : options.ScaleUpThreshold;
                });

            if (!string.IsNullOrWhiteSpace(boltConfig.AccessToken))
            {
                builder.WithAccessToken(boltConfig.AccessToken);
            }
            else if (!boltConfig.Anonymous)
            {
                builder.WithAccessTokenProvider<IBoltTransportTokenProvider>(
                    async (provider, ct) => await provider.GetTokenAsync(ct));
            }

            if (!autoConnect || connectAfterApplicationStarted)
                builder.DisableAutoConnect();
        });

        if (autoConnect && connectAfterApplicationStarted)
            services.AddHostedService<ApplicationStartedBoltClientHostedService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBoltServiceManifestProvider, ConfigurationBoltServiceManifestProvider>());
        services.AddHostedService<BoltServiceManifestAdvertisementHostedService>();
        services.TryAddSingleton<IIdentitySigningKeyProvider, IdentityServerSigningKeyProvider>();
        services.TryAddSingleton<IServiceTokenValidator, ServiceTokenValidator>();
        services.TryAddSingleton<ITrustedServiceInvocationResolver, TrustedServiceInvocationResolver>();
        services.AddSingleton<IMessageBusWrapper, BoltDriver>();

        return services;
    }

    private static bool UsesCentralTransportIdentity(BoltConfiguration boltConfig) =>
        !boltConfig.Anonymous && string.IsNullOrWhiteSpace(boltConfig.AccessToken);

    private static void ValidateTransportIdentityModes(
        BoltConfiguration boltConfig,
        IHostEnvironment? hostEnvironment)
    {
        var isDevelopment = hostEnvironment?.IsDevelopment() == true;

        if (boltConfig.Anonymous && !isDevelopment)
        {
            throw new InvalidOperationException(
                "BoltConfiguration:Anonymous is permitted only when the host environment is Development.");
        }

        if (boltConfig.GenerateServiceAccessToken)
        {
            throw new InvalidOperationException(
                "BoltConfiguration:GenerateServiceAccessToken is no longer supported. " +
                "Use IdentityServer-issued Bolt transport tokens.");
        }

        if (!string.IsNullOrWhiteSpace(boltConfig.AccessToken) &&
            boltConfig.Anonymous)
        {
            throw new InvalidOperationException(
                "BoltConfiguration:AccessToken cannot be combined with anonymous transport identity mode.");
        }
    }

    public static IServiceCollection AddBoltServiceManifestProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IBoltServiceManifestProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBoltServiceManifestProvider, TProvider>());
        return services;
    }

    /// <summary>
    /// Registers RemoteDataContext as the IDataContext implementation for remote/WASM clients.
    /// </summary>
    public static IServiceCollection AddRemoteDataContext(this IServiceCollection services)
    {
        services.AddScoped<IDataContext>(sp =>
        {
            var metadata = sp.GetService<RequestMetadata>() ?? new RequestMetadata();
            return new RemoteDataContext(sp, metadata);
        });
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

internal sealed class ApplicationStartedBoltClientHostedService(
    BoltClient client,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ApplicationStartedBoltClientHostedService> logger) : IHostedService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _stoppingCts;
    private CancellationTokenRegistration _applicationStartedRegistration;
    private Task _connectTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);
        _applicationStartedRegistration = applicationLifetime.ApplicationStarted.Register(StartConnection);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _applicationStartedRegistration.Dispose();
        _stoppingCts?.Cancel();

        Task connectTask;
        lock (_gate)
            connectTask = _connectTask;

        try
        {
            await connectTask.WaitAsync(ct);
        }
        catch (OperationCanceledException) when (_stoppingCts?.IsCancellationRequested == true || ct.IsCancellationRequested)
        {
        }

        await client.DisposeAsync();
        _stoppingCts?.Dispose();
    }

    private void StartConnection()
    {
        lock (_gate)
        {
            if (!_connectTask.IsCompleted || _stoppingCts is null || _stoppingCts.IsCancellationRequested)
                return;

            _connectTask = ConnectAsync(_stoppingCts.Token);
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Bolt client connecting after application startup...");
                await client.ConnectWithRetryAsync(ct);
                if (client.IsConnected)
                {
                    logger.LogInformation("Bolt client connected after application startup");
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Bolt client connection canceled during application shutdown");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bolt client startup retry cycle failed; retrying until shutdown");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
