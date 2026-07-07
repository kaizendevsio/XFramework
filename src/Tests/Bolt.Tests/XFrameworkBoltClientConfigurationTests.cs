using System.Reflection;
using Bolt.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XFramework.Integration.Extensions;

namespace Bolt.Tests;

[TestFixture]
public class XFrameworkBoltClientConfigurationTests
{
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

    private static BoltClientOptions GetOptions(BoltClient client)
    {
        var field = typeof(BoltClient).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (BoltClientOptions)field!.GetValue(client)!;
    }
}
