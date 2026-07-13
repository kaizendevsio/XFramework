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
using XFramework.Integration.Extensions;
using XFramework.Integration.Health;

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

    [TestCase("Staging")]
    [TestCase("Production")]
    public void AddXFrameworkBoltClient_NonDevelopmentPlaintextUrl_Throws(string environmentName)
    {
        var configuration = CreateConfiguration("ws://localhost:9999/bolt", requireSecureTransport: false);
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: new TestHostEnvironment(environmentName));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use wss:// or https://*secure transport is required*");
    }

    [Test]
    public void AddXFrameworkBoltClient_DevelopmentPlaintextUrl_Succeeds()
    {
        var configuration = CreateConfiguration("ws://localhost:9999/bolt", requireSecureTransport: false);
        var services = CreateServices();

        var act = () => services.AddXFrameworkBoltClient(
            configuration,
            autoConnect: false,
            hostEnvironment: new TestHostEnvironment(Environments.Development));

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
                ["BoltConfiguration:Anonymous"] = "true",
                ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
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

    private static IConfigurationRoot CreateConfiguration(string serverUrl, bool requireSecureTransport)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ServerUrls:0"] = serverUrl,
                ["BoltConfiguration:ClientName"] = "Bolt.TestClient",
                ["BoltConfiguration:Anonymous"] = "true",
                ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
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

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(XFrameworkBoltClientConfigurationTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
