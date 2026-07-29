using Bolt.Client;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Hub.Security;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltServiceDiscoveryIntegrationTests
{
    private static int _portCounter = 20200;
    private const string JuanBarangayServiceName = "XFramework.JuanBarangay";
    private WebApplication _hubApp = null!;
    private ILoggerFactory _loggerFactory = null!;
    private string _databaseName = string.Empty;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        _databaseName = $"bolt-discovery-{Guid.NewGuid():N}";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddDbContext<DbContext, AppDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName));
        builder.Services.AddBoltServer(options =>
            options.RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce);
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IBoltServicePresenceTracker, BoltServicePresenceTracker>();
        builder.Services.AddScoped<IBoltServiceDiscoveryRegistry, BoltServiceDiscoveryRegistry>();
        builder.Services.AddHostedService<BoltServiceDiscoveryHostedService>();
        builder.Services.AddAuthorization(BoltAuthorizationPolicies.AddServiceDiscoveryReaderPolicy);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.Use(async (context, next) =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var principalToken = authorization["Bearer ".Length..].Trim();
                if (principalToken.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
                {
                    context.User = CreateAuthenticatedUserPrincipal(principalToken["user:".Length..]);
                }
                else if (!string.IsNullOrWhiteSpace(principalToken))
                {
                    context.User = CreateServicePrincipal(principalToken);
                }
            }

            await next();
        });
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
        _loggerFactory = _hubApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _hubApp.StopAsync(); } catch { }
        try { await _hubApp.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task AdvertiseServiceManifest_ThroughHubLocalHandler_PersistsSenderClientAndReturnsRegistry()
    {
        var client = CreateServiceClient(JuanBarangayServiceName);
        await client.ConnectAsync();

        var response = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());

        response.Should().NotBeNull();
        response!.Accepted.Should().BeTrue();

        var modules = await client.SendAsync<BoltModuleRegistryRequest, BoltModuleRegistryResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.GetModuleRegistry,
            new BoltModuleRegistryRequest { IncludeOffline = true });

        modules!.Modules.Should().ContainSingle(module => module.ModuleKey == "juan_barangay");
        modules.Modules.Single(module => module.ModuleKey == "juan_barangay")
            .Features.Should().Contain(feature => feature.Key == "juan_barangay.residents");

        using var scope = _hubApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName));
        record.ClientName.Should().Be(JuanBarangayServiceName);
        record.ServiceName.Should().Be(JuanBarangayServiceName);
        record.IsConnected.Should().BeTrue();

        await client.DisposeAsync();
    }

    [Test]
    public async Task AdvertiseServiceManifest_UnauthenticatedClient_IsRejectedAndDoesNotPersistManifestModules()
    {
        var client = CreateClient("portal_user", "PortalUser");
        await client.ConnectAsync();

        var response = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());

        response.Should().NotBeNull();
        response!.Accepted.Should().BeFalse();
        response.Message.Should().Contain("Authenticated service identity");

        await WaitUntilAsync(async () =>
        {
            using var waitScope = _hubApp.Services.CreateScope();
            var waitDb = waitScope.ServiceProvider.GetRequiredService<DbContext>();
            return await waitDb.Set<BoltServiceManifestRecord>().AnyAsync(x => x.ClientId == "portal_user");
        });

        using var scope = _hubApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "portal_user");
        record.ManifestJson.Should().NotContain("juan_barangay");

        await client.DisposeAsync();
    }

    [Test]
    public async Task AdvertiseServiceManifest_AuthenticatedUserWithoutServiceScope_IsRejectedAndDoesNotPersistManifestModules()
    {
        var client = CreateClient("normal_user", "NormalUser", "user:normal-user");
        await client.ConnectAsync();

        var response = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());

        response.Should().NotBeNull();
        response!.Accepted.Should().BeFalse();
        response.Message.Should().Contain("bolt.service scope");

        await WaitUntilAsync(async () =>
        {
            using var waitScope = _hubApp.Services.CreateScope();
            var waitDb = waitScope.ServiceProvider.GetRequiredService<DbContext>();
            return await waitDb.Set<BoltServiceManifestRecord>().AnyAsync(x => x.ClientId == "normal_user");
        });

        using var scope = _hubApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "normal_user");
        record.ManifestJson.Should().NotContain("juan_barangay");

        await client.DisposeAsync();
    }

    [Test]
    public async Task AdvertiseServiceManifest_MismatchedServiceName_IsRejectedAndDoesNotOverwriteExistingManifest()
    {
        var client = CreateServiceClient(JuanBarangayServiceName);
        await client.ConnectAsync();

        var accepted = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());
        accepted!.Accepted.Should().BeTrue();

        var spoofed = CreateJuanBarangayManifest();
        spoofed.ServiceName = XFrameworkServiceNames.IdentityServer;

        var rejected = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            spoofed);

        rejected.Should().NotBeNull();
        rejected!.Accepted.Should().BeFalse();
        rejected.Message.Should().Contain("Manifest service name");

        using var scope = _hubApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName));
        record.ServiceName.Should().Be(JuanBarangayServiceName);
        record.ManifestJson.Should().NotContain(XFrameworkServiceNames.IdentityServer);

        await client.DisposeAsync();
    }

    [Test]
    public async Task ConnectAsync_ServiceTokenForDifferentService_RejectsRegistration()
    {
        var client = CreateServiceClient(
            XFrameworkServiceNames.IdentityServer,
            accessTokenServiceName: XFrameworkServiceNames.Wallets);

        try
        {
            await FluentActions.Invoking(() => client.ConnectAsync())
                .Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*rejected registration*");
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Test]
    public async Task Disconnect_AfterManifestAdvertisement_KeepsManifestAndMarksServiceOffline()
    {
        var client = CreateServiceClient(JuanBarangayServiceName);
        await client.ConnectAsync();
        await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());

        await client.DisposeAsync();

        await WaitUntilAsync(async () =>
        {
            using var scope = _hubApp.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName));
            return !record.IsConnected && record.ConnectionCount == 0 && record.ManifestJson.Contains("juan_barangay");
        });
    }

    [Test]
    public async Task ResetPresenceAsync_KeepsManifestAndMarksPersistedOnlineServicesOffline()
    {
        var manifestJson = JsonSerializer.Serialize(
            CreateJuanBarangayManifest(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using (var scope = _hubApp.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            db.Add(new BoltServiceManifestRecord
            {
                Id = Guid.NewGuid(),
                ClientId = "stale_client",
                ClientName = "StaleService",
                ServiceName = "Juan_Barangay_Service",
                DisplayName = "Juan Barangay",
                Version = "1.0.0",
                IsConnected = true,
                ConnectionCount = 2,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-5),
                LastConnectedAt = DateTime.UtcNow.AddMinutes(-5),
                ManifestHash = "stale-hash",
                ManifestJson = manifestJson,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _hubApp.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
            await registry.ResetPresenceAsync(CancellationToken.None);
        }

        using (var scope = _hubApp.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "stale_client");
            record.IsConnected.Should().BeFalse();
            record.ConnectionCount.Should().Be(0);
            record.ManifestJson.Should().Be(manifestJson);
            record.LastDisconnectedAt.Should().NotBeNull();
        }
    }

    [Test]
    public async Task RetireStaleAsync_RemovesOnlyOfflineRecordsOlderThanRetention()
    {
        var manifestJson = JsonSerializer.Serialize(
            CreateJuanBarangayManifest(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using (var scope = _hubApp.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            db.AddRange(
                CreateOfflineRecord("expired_offline", DateTime.UtcNow.AddDays(-31), manifestJson),
                CreateOfflineRecord("retained_offline", DateTime.UtcNow.AddDays(-29), manifestJson));
            await db.SaveChangesAsync();
        }

        using (var scope = _hubApp.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
            await registry.RetireStaleAsync(CancellationToken.None);
        }

        using (var scope = _hubApp.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            (await db.Set<BoltServiceManifestRecord>().AnyAsync(x => x.ClientId == "expired_offline"))
                .Should().BeFalse();
            (await db.Set<BoltServiceManifestRecord>().AnyAsync(x => x.ClientId == "retained_offline"))
                .Should().BeTrue();
        }
    }

    [Test]
    public async Task MultipleConnectionsForSameClient_DisconnectingOneKeepsServiceOnlineUntilFinalConnectionCloses()
    {
        var firstClient = CreateClient("pooled_client", "PooledService");
        var secondClient = CreateClient("pooled_client", "PooledService");

        try
        {
            await Task.WhenAll(firstClient.ConnectAsync(), secondClient.ConnectAsync());

            await WaitUntilAsync(async () =>
            {
                using var scope = _hubApp.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContext>();
                var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "pooled_client");
                return record.IsConnected && record.ConnectionCount == 2;
            });

            await firstClient.DisposeAsync();

            await WaitUntilAsync(async () =>
            {
                using var scope = _hubApp.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContext>();
                var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "pooled_client");
                return record.IsConnected && record.ConnectionCount == 1;
            });

            await secondClient.DisposeAsync();

            await WaitUntilAsync(async () =>
            {
                using var scope = _hubApp.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContext>();
                var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == "pooled_client");
                return !record.IsConnected && record.ConnectionCount == 0;
            });
        }
        finally
        {
            await firstClient.DisposeAsync();
            await secondClient.DisposeAsync();
        }
    }

    [Test]
    public async Task Reconnect_AfterDisconnect_MarksServiceOnlineAndUpdatesLastSeen()
    {
        var firstClient = CreateServiceClient(JuanBarangayServiceName);
        await firstClient.ConnectAsync();
        await firstClient.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            CreateJuanBarangayManifest());
        await firstClient.DisposeAsync();

        await WaitUntilAsync(async () =>
        {
            using var scope = _hubApp.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName));
            return !record.IsConnected;
        });

        DateTime offlineSeenAt;
        using (var scope = _hubApp.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            offlineSeenAt = (await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName))).LastSeenAt;
        }

        var secondClient = CreateServiceClient(JuanBarangayServiceName);
        await secondClient.ConnectAsync();

        await WaitUntilAsync(async () =>
        {
            using var scope = _hubApp.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var record = await db.Set<BoltServiceManifestRecord>().SingleAsync(x => x.ClientId == Sha256Hex(JuanBarangayServiceName));
            return record.IsConnected && record.ConnectionCount == 1 && record.LastSeenAt >= offlineSeenAt;
        });

        await secondClient.DisposeAsync();
    }

    [Test]
    public async Task GetModuleRegistry_RequiredMissingServiceDependency_ReturnsDegradedModuleAndFeature()
    {
        var client = CreateServiceClient(JuanBarangayServiceName);
        await client.ConnectAsync();
        var manifest = CreateJuanBarangayManifest();
        manifest.Modules[0].Dependencies.Add(new BoltDependencyRequirement
        {
            Kind = BoltDependencyKind.Service,
            Key = "missing_service",
            DisplayName = "Missing Service",
            Required = true
        });

        await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            manifest);

        var modules = await client.SendAsync<BoltModuleRegistryRequest, BoltModuleRegistryResponse>(
            string.Empty,
            BoltServiceDiscoveryCommands.GetModuleRegistry,
            new BoltModuleRegistryRequest { IncludeOffline = true });

        var module = modules!.Modules.Single(x => x.ModuleKey == "juan_barangay");
        module.Status.Should().Be(BoltRegistryStatus.Degraded);
        module.DependencyStatuses.Should().Contain(status =>
            status.Requirement.Key == "missing_service" && !status.IsSatisfied && status.Requirement.Required);
        module.Features.Should().Contain(feature =>
            feature.Key == "juan_barangay.residents" && feature.Status == BoltRegistryStatus.Degraded);

        await client.DisposeAsync();
    }

    private BoltClient CreateServiceClient(string serviceName, string? accessTokenServiceName = null) =>
        CreateClient(Sha256Hex(serviceName), serviceName, accessTokenServiceName ?? serviceName);

    private BoltClient CreateClient(string id, string name, string? serviceIdentityName = null) =>
        new(
            new Uri($"ws://localhost:{_port}/bolt"),
            id,
            name,
            new BoltClientOptions
            {
                RpcTimeoutSeconds = 5,
                AccessToken = serviceIdentityName
            },
            _loggerFactory.CreateLogger<BoltClient>());

    private static ClaimsPrincipal CreateServicePrincipal(string serviceName)
    {
        List<Claim> claims =
        [
            new("client_id", serviceName),
            new("service", serviceName),
            new("scope", XFrameworkServiceScopes.BoltService),
            new(ClaimTypes.Name, serviceName)
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestService"));
    }

    private static ClaimsPrincipal CreateAuthenticatedUserPrincipal(string userName)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.NameIdentifier, userName)
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestUser"));
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static BoltServiceManifestRecord CreateOfflineRecord(
        string clientId,
        DateTime lastSeenAt,
        string manifestJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ClientName = clientId,
            ServiceName = clientId,
            DisplayName = clientId,
            Version = "1.0.0",
            IsConnected = false,
            ConnectionCount = 0,
            LastSeenAt = lastSeenAt,
            LastDisconnectedAt = lastSeenAt,
            ManifestHash = clientId,
            ManifestJson = manifestJson,
            CreatedAt = lastSeenAt
        };

    private static BoltServiceManifest CreateJuanBarangayManifest() =>
        new()
        {
            ServiceName = JuanBarangayServiceName,
            DisplayName = "Juan Barangay",
            Version = "1.0.0",
            Modules =
            [
                new BoltModuleManifest
                {
                    ModuleKey = "Juan_Barangay",
                    DisplayName = "Juan Barangay",
                    Description = "Barangay resident operations.",
                    IconName = "users",
                    Features =
                    [
                        new BoltTenantModuleFeatureManifest
                        {
                            Key = "Juan_Barangay.Residents",
                            DisplayName = "Residents",
                            Description = "Resident registry.",
                            IconName = "users"
                        }
                    ]
                }
            ]
        };

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }
}
