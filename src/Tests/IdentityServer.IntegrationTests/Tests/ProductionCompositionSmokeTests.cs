using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
public sealed class ProductionCompositionSmokeTests
{
    [Test]
    public async Task Program_ComposesProductionRegistrationsEndpointsAndValidation()
    {
        var keyDirectory = Path.Combine(
            Path.GetTempPath(),
            "XFramework.IdentityServer.ProductionComposition",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyDirectory);
        WriteProvisionedKeyPair(keyDirectory);

        try
        {
            using var environment = new EnvironmentVariableScope(CreateConfiguration(keyDirectory));
            await using var factory = new WebApplicationFactory<ServiceIdentityService>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        var redisDatabase = new Mock<IDatabase>();
                        redisDatabase
                            .Setup(database => database.PingAsync(It.IsAny<CommandFlags>()))
                            .ReturnsAsync(TimeSpan.Zero);
                        redisDatabase
                            .Setup(database => database.ScriptEvaluateAsync(
                                It.IsAny<string>(),
                                It.IsAny<RedisKey[]>(),
                                It.IsAny<RedisValue[]>(),
                                It.IsAny<CommandFlags>()))
                            .ReturnsAsync(RedisResult.Create([(RedisValue)1L, (RedisValue)60_000L]));
                        var redis = new Mock<IConnectionMultiplexer>();
                        redis.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                            .Returns(redisDatabase.Object);
                        services.RemoveAll<IConnectionMultiplexer>();
                        services.AddSingleton(redis.Object);
                    });
                });

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://identityserver.test")
            });
            factory.Services.GetRequiredService<IServiceIdentityService>()
                .Should().BeOfType<ServiceIdentityService>();
            factory.Services.GetServices<IHostedService>()
                .Select(service => service.GetType().Name)
                .Should().Contain(
                    nameof(PasswordResetOutboxDispatcher),
                    nameof(VerificationDeliveryOutboxDispatcher),
                    nameof(StorageCleanupOutboxDispatcher),
                    nameof(StorageClaimOutboxDispatcher));

            using var response = await client.PostAsJsonAsync(
                "/api/service-identity/token",
                new IssueServiceTokenRequest
                {
                    ClientId = "composition-client",
                    ClientSecret = "composition-client-secret-material-2026",
                    Audience = XFrameworkServiceNames.IdentityServer,
                    Scopes = null!
                });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }
        finally
        {
            if (Directory.Exists(keyDirectory))
                Directory.Delete(keyDirectory, recursive: true);
        }
    }

    private static Dictionary<string, string?> CreateConfiguration(string keyDirectory) => new()
    {
        ["DOTNET_ENVIRONMENT"] = "Production",
        ["ASPNETCORE_ENVIRONMENT"] = "Production",
        ["DefaultDatabaseConnection"] =
            "Host=127.0.0.1;Port=1;Database=composition;Username=composition;Password=composition",
        ["DistributedSecurityRateLimiting:Enabled"] = "true",
        ["DistributedSecurityRateLimiting:RedisConnectionString"] = "localhost:6379",
        ["BoltConfiguration:ClientName"] = "XFramework.IdentityServer.CompositionTest",
        ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
        ["BoltConfiguration:ServerUrls:0"] = "ws://127.0.0.1:1/bolt/ws",
        ["BoltConfiguration:RequireSecureTransport"] = "false",
        ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
        ["ServiceIdentity:ClientId"] = "composition-client",
        ["ServiceIdentity:GenerationId"] = "composition-client-g1",
        ["ServiceIdentity:ClientSecret"] = "composition-client-secret-material-2026",
        ["ServiceIdentity:Authority"] = "https://identityserver.test",
        ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
        ["ServiceIdentity:BoltTransportTokenIssuer:Enabled"] = "false",
        ["ServiceIdentity:Clients:0:ClientId"] = "composition-client",
        ["ServiceIdentity:Clients:0:GenerationId"] = "composition-client-g1",
        ["ServiceIdentity:Clients:0:ClientSecret"] = "composition-client-secret-material-2026",
        ["ServiceIdentity:Clients:0:AllowedAudiences"] = XFrameworkServiceNames.IdentityServer,
        ["ServiceIdentity:Clients:0:AllowedScopes"] = XFrameworkServiceScopes.BoltService,
        ["JwtOptions:ValidAudience"] = "https://identityserver.test",
        ["JwtOptions:ValidIssuer"] = "https://identityserver.test",
        ["JwtOptions:GenerationId"] = "composition-user-jwt-g1",
        ["JwtOptions:SigningPrivateKeyPath"] = Path.Combine(keyDirectory, "private.pem"),
        ["JwtOptions:SigningPublicKeyPath"] = Path.Combine(keyDirectory, "public.pem")
    };

    private static void WriteProvisionedKeyPair(string keyDirectory)
    {
        using var rsa = RSA.Create(3_072);
        File.WriteAllText(
            Path.Combine(keyDirectory, "private.pem"),
            rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(
            Path.Combine(keyDirectory, "public.pem"),
            rsa.ExportSubjectPublicKeyInfoPem());
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new(StringComparer.Ordinal);

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> settings)
        {
            foreach (var (key, value) in settings)
            {
                var environmentKey = key.Replace(":", "__", StringComparison.Ordinal);
                _previousValues[environmentKey] = Environment.GetEnvironmentVariable(environmentKey);
                Environment.SetEnvironmentVariable(environmentKey, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
