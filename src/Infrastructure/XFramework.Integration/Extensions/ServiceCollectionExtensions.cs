using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Bolt.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.DataContext;
using XFramework.Integration.Drivers;
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
        bool autoConnect = true)
    {
        services.Configure<BoltConfiguration>(configuration.GetSection("BoltConfiguration"));
        services.Configure<ServiceIdentityOptions>(options =>
        {
            configuration.GetSection(ServiceIdentityOptions.SectionName).Bind(options);
            options.ClientId = string.IsNullOrWhiteSpace(options.ClientId)
                ? configuration["BoltConfiguration:ClientName"]
                : options.ClientId;

            if (options.DefaultScopes.Count == 0)
                options.DefaultScopes = XFrameworkServiceScopes.AdminDefaults.ToList();
        });
        services.Configure<BoltServiceDiscoveryOptions>(
            configuration.GetSection(BoltServiceDiscoveryOptions.SectionName));

        var boltConfig = configuration.GetSection("BoltConfiguration").Get<BoltConfiguration>()
            ?? throw new InvalidOperationException("BoltConfiguration section is missing or empty in configuration.");

        if (boltConfig.ServerUrls is null || boltConfig.ServerUrls.Count == 0)
            throw new InvalidOperationException("BoltConfiguration:ServerUrls must contain at least one URL.");

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
                    if (!string.IsNullOrWhiteSpace(boltConfig.AccessToken))
                    {
                        options.AccessToken = boltConfig.AccessToken;
                        return;
                    }

                    if (!boltConfig.Anonymous && boltConfig.GenerateServiceAccessToken)
                    {
                        options.AccessTokenProvider = _ =>
                            new ValueTask<string?>(GenerateBoltServiceAccessToken(configuration, clientName));
                    }
                });

            if (!autoConnect)
                builder.DisableAutoConnect();
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBoltServiceManifestProvider, ConfigurationBoltServiceManifestProvider>());
        services.AddHostedService<BoltServiceManifestAdvertisementHostedService>();
        services.TryAddSingleton<IServiceTokenProvider, IdentityServerServiceTokenProvider>();
        services.TryAddSingleton<IIdentitySigningKeyProvider, IdentityServerSigningKeyProvider>();
        services.TryAddSingleton<IServiceTokenValidator, ServiceTokenValidator>();
        services.TryAddSingleton<ITrustedServiceInvocationResolver, TrustedServiceInvocationResolver>();
        services.AddSingleton<IMessageBusWrapper, BoltDriver>();

        return services;
    }

    private static string GenerateBoltServiceAccessToken(IConfiguration configuration, string clientName)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JwtOptions is required to generate Bolt service access tokens.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret)
            || string.IsNullOrWhiteSpace(jwtOptions.ValidAudience)
            || string.IsNullOrWhiteSpace(jwtOptions.ValidIssuer))
        {
            throw new InvalidOperationException("JwtOptions Secret, ValidAudience, and ValidIssuer are required to generate Bolt service access tokens.");
        }

        var lifetime = TimeSpan.TryParse(jwtOptions.AccessTokenLifespan, out var parsedLifetime)
            ? parsedLifetime
            : TimeSpan.FromMinutes(30);

        List<Claim> claims =
        [
            new("client_id", clientName),
            new("service", clientName),
            new("scope", "bolt.service"),
            new(ClaimTypes.Name, clientName),
            new(JwtRegisteredClaimNames.Sub, clientName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.AuthTime, DateTime.UtcNow.ToString("O"))
        ];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));
        var token = new JwtSecurityToken(
            issuer: jwtOptions.ValidIssuer,
            audience: jwtOptions.ValidAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512));

        return new JwtSecurityTokenHandler().WriteToken(token);
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
