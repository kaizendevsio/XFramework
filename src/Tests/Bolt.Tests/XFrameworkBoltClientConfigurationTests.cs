using System.Reflection;
using System.Text.Json;
using Bolt.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Extensions;
using XFramework.Integration.Health;
using XFramework.Integration.Security;

namespace Bolt.Tests;

[TestFixture]
public class XFrameworkBoltClientConfigurationTests
{
    [TestCase("ws://localhost:9999/bolt", "ws")]
    [TestCase("http://localhost:9999/bolt", "http")]
    public void AddXFrameworkBoltClient_SecureTransportRequiredWithPlaintextUrl_Throws(
        string serverUrl,
        string scheme)
    {
        var configuration = CreateConfiguration(serverUrl, requireSecureTransport: true);
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(configuration, autoConnect: false);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                $"*must use wss:// or https://*secure transport is required*scheme '{scheme}' is not secure*");
    }

    [TestCase("wss://localhost:9999/bolt")]
    [TestCase("https://localhost:9999/bolt")]
    public void AddXFrameworkBoltClient_SecureTransportRequiredWithSecureUrl_Succeeds(string serverUrl)
    {
        var configuration = CreateConfiguration(serverUrl, requireSecureTransport: true);
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(configuration, autoConnect: false);

        act.Should().NotThrow();
    }

    [Test]
    public void AddXFrameworkBoltClient_SecureTransportRequiredWithAnyPlaintextUrl_Throws()
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        configuration["BoltConfiguration:ServerUrls:1"] = "http://localhost:9998/bolt";
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(configuration, autoConnect: false);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("BoltConfiguration:ServerUrls:1 must use wss:// or https://*");
    }

    [TestCase("Development")]
    [TestCase("Staging")]
    [TestCase("Production")]
    public void AddXFrameworkBoltClient_SecureTransportExplicitlyDisabled_AllowsPlaintextUrl(
        string environmentName)
    {
        var configuration = CreateConfiguration("ws://localhost:9999/bolt", requireSecureTransport: false);
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: new TestHostEnvironment(environmentName));

        act.Should().NotThrow();
    }

    [Test]
    public async Task AddXFrameworkBoltClient_AppliesBoltConfigurationPoolSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ServerUrls:0"] = "ws://localhost:9999/bolt",
                ["BoltConfiguration:ClientName"] = "Bolt.TestClient",
                ["BoltConfiguration:MinConnections"] = "3",
                ["BoltConfiguration:MaxConnections"] = "7",
                ["BoltConfiguration:ScaleUpThreshold"] = "11"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddXFrameworkBoltClient(configuration, autoConnect: false);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<BoltClient>();
        var options = GetOptions(client);

        options.MinConnections.Should().Be(3);
        options.MaxConnections.Should().Be(7);
        options.ScaleUpThreshold.Should().Be(11);
    }

    [Test]
    public async Task AddXFrameworkBoltClient_RegistersDetailedTransportReadinessCheck()
    {
        var configuration = CreateConfiguration("ws://localhost:9999/bolt", requireSecureTransport: false);
        var services = CreateServices();

        services.AddXFrameworkBoltClient(configuration, autoConnect: false);

        await using var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Single(item => item.Name == BoltClientTransportHealthCheckExtensions.RegistrationName);
        var check = (BoltClientTransportHealthCheck)registration.Factory(provider);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        registration.FailureStatus.Should().Be(HealthStatus.Unhealthy);
        registration.Tags.Should().Contain(["bolt", "transport", "client", "ready"]);
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().ContainSingle().Which.Key.Should().Be("transport");
        var snapshot = result.Data["transport"].Should().BeOfType<BoltClientHealthSnapshot>().Subject;
        var serialized = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        serialized.GetProperty("isHealthy").GetBoolean().Should().BeFalse();
        serialized.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "isRegistered",
            "connectionCount",
            "connectedTransports",
            "pendingSends",
            "activeSends",
            "maxActiveSendElapsedMs",
            "runningSendLoops",
            "runningReceiveLoops",
            "faultedSendLoops",
            "faultedReceiveLoops",
            "pendingSendsUnhealthyThreshold",
            "activeSendUnhealthyThresholdMs",
            "totalSendFailures",
            "totalSendTimeouts",
            "totalReceiveLoopFaults",
            "totalUnexpectedDisconnects",
            "totalSuccessfulReconnects",
            "isHealthy");
    }

    [Test]
    public async Task AddXFrameworkBoltClient_DefaultTransportIdentity_ResolvesProviderAtConnectionTime()
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        var services = CreateServices();
        var constructed = 0;
        services.AddSingleton<IBoltTransportTokenProvider>(_ =>
        {
            Interlocked.Increment(ref constructed);
            return new StubBoltTransportTokenProvider("central-token");
        });

        services.AddXFrameworkBoltClient(configuration, autoConnect: false);
        await using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<BoltClient>();
        var options = GetOptions(client);

        constructed.Should().Be(0);
        options.AccessTokenProvider.Should().NotBeNull();
        var token = await options.AccessTokenProvider!(CancellationToken.None);

        token.Should().Be("central-token");
        constructed.Should().Be(1);
    }

    [Test]
    public async Task AddXFrameworkBoltClient_ServiceIdentityHttpClient_HasInfiniteBuiltInTimeout()
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        configuration["ServiceIdentity:Authority"] = "https://identity.test";
        configuration["ServiceIdentity:ClientId"] = "Bolt.TestClient";
        configuration["ServiceIdentity:GenerationId"] = "test-g0";
        configuration["ServiceIdentity:ClientSecret"] = "test-client-secret-material-at-least-32-bytes";
        configuration["ServiceIdentity:DefaultScopes:0"] = "bolt.test";
        var services = CreateServices();

        services.AddXFrameworkBoltClient(configuration, autoConnect: false);
        await using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(ServiceIdentityHttpClient.Name);

        client.BaseAddress.Should().Be("https://identity.test/");
        client.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Test]
    public async Task AddXFrameworkBoltClient_ConnectAfterApplicationStarted_StartsWithoutHubAndOmitsTransportReadiness()
    {
        var configuration = CreateConfiguration("wss://127.0.0.1:1/bolt", requireSecureTransport: true);
        configuration["ServiceIdentity:Authority"] = "https://identity.test";
        configuration["ServiceIdentity:ClientId"] = "Bolt.TestClient";
        configuration["ServiceIdentity:GenerationId"] = "test-g0";
        configuration["ServiceIdentity:ClientSecret"] = "test-client-secret-material-at-least-32-bytes";
        configuration["ServiceIdentity:DefaultScopes:0"] = "bolt.test";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: true,
            hostEnvironment: builder.Environment,
            connectAfterApplicationStarted: true);
        using var host = builder.Build();

        var hostedServiceNames = GetHostedServiceNames(builder.Services);
        hostedServiceNames.Should().Contain("ApplicationStartedBoltClientHostedService");
        hostedServiceNames.Should().NotContain("BoltClientHostedService");
        host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Should().NotContain(
                registration => registration.Name == BoltClientTransportHealthCheckExtensions.RegistrationName);

        var start = async () => await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await start.Should().NotThrowAsync(
            "IdentityServer must serve HTTP before its downstream Bolt Hub is available");
        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var stopAgain = async () => await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await stopAgain.Should().NotThrowAsync("hosted-service shutdown must be idempotent");
    }

    [Test]
    public void AddXFrameworkBoltClient_AutoConnectDisabled_DoesNotRegisterAnyConnector()
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        var services = CreateServices();

        services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            connectAfterApplicationStarted: true);

        var hostedServiceNames = GetHostedServiceNames(services);
        hostedServiceNames.Should().NotContain("ApplicationStartedBoltClientHostedService");
        hostedServiceNames.Should().NotContain("BoltClientHostedService");
    }

    [TestCase(null)]
    [TestCase("Staging")]
    [TestCase("Production")]
    public void AddXFrameworkBoltClient_AnonymousOutsideDevelopment_Throws(string? environmentName)
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        configuration["BoltConfiguration:Anonymous"] = "true";
        var services = CreateServices();
        IHostEnvironment? environment = environmentName is null
            ? null
            : new TestHostEnvironment(environmentName);

        var act = () => services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Anonymous*only*Development*");
    }

    [Test]
    public async Task AddXFrameworkBoltClient_AnonymousInDevelopment_HostStartsWithoutServiceIdentityConfiguration()
    {
        var configuration = CreateConfiguration("ws://localhost:9999/bolt", requireSecureTransport: false);
        configuration["BoltConfiguration:Anonymous"] = "true";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: builder.Environment);
        using var host = builder.Build();
        var act = () => host.StartAsync();

        await act.Should().NotThrowAsync();
        await host.StopAsync();
    }

    [TestCase("Development")]
    [TestCase("Production")]
    public void AddXFrameworkBoltClient_GenerateServiceAccessTokenInAnyEnvironment_Throws(
        string environmentName)
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        configuration["BoltConfiguration:GenerateServiceAccessToken"] = "true";
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: new TestHostEnvironment(environmentName));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GenerateServiceAccessToken*no longer supported*IdentityServer-issued*");
    }

    [Test]
    public async Task AddXFrameworkBoltClient_StaticAccessToken_HostStartsWithoutServiceIdentityConfiguration()
    {
        var configuration = CreateConfiguration("wss://localhost:9999/bolt", requireSecureTransport: true);
        configuration["BoltConfiguration:AccessToken"] = "centrally-issued-synthetic";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: builder.Environment);
        using var host = builder.Build();
        var act = () => host.StartAsync();

        await act.Should().NotThrowAsync();

        var options = GetOptions(host.Services.GetRequiredService<BoltClient>());
        options.AccessToken.Should().Be("centrally-issued-synthetic");
        options.AccessTokenProvider.Should().BeNull();
        await host.StopAsync();
    }

    [Test]
    public void BoltConfiguration_GenerateServiceAccessToken_DefaultsToFalse()
    {
        new BoltConfiguration().GenerateServiceAccessToken.Should().BeFalse();
    }

    [TestCase("http://identity.test", false, false)]
    [TestCase("http://identity.test", true, true)]
    [TestCase("https://identity.test", false, true)]
    public void ServiceIdentityOptionsValidator_AuthorityPolicy_ReturnsExpectedResult(
        string authority,
        bool allowInsecureHttp,
        bool expectedSuccess)
    {
        var options = new ServiceIdentityOptions
        {
            Authority = authority,
            AllowInsecureHttp = allowInsecureHttp,
            ClientId = "Bolt.TestClient",
            GenerationId = "test-g0",
            ClientSecret = "test-client-secret-material-at-least-32-bytes",
            DefaultScopes = ["bolt.test"]
        };
        var validator = new ServiceIdentityOptionsValidator(TimeProvider.System);

        var result = validator.Validate(null, options);

        result.Succeeded.Should().Be(expectedSuccess);
        if (!expectedSuccess)
            result.FailureMessage.Should().Contain("must use HTTPS");
    }

    private static IConfigurationRoot CreateConfiguration(string serverUrl, bool requireSecureTransport)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ServerUrls:0"] = serverUrl,
                ["BoltConfiguration:ClientName"] = "Bolt.TestClient",
                ["BoltConfiguration:RequireSecureTransport"] = requireSecureTransport.ToString()
            })
            .Build();
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static BoltClientOptions GetOptions(BoltClient client)
    {
        var field = typeof(BoltClient).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (BoltClientOptions)field!.GetValue(client)!;
    }

    private static IReadOnlyCollection<string> GetHostedServiceNames(IServiceCollection services) =>
        services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType?.Name)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToArray();

    private sealed class StubBoltTransportTokenProvider(string token) : IBoltTransportTokenProvider
    {
        public ValueTask<string> GetTokenAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(token);
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(XFrameworkBoltClientConfigurationTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
